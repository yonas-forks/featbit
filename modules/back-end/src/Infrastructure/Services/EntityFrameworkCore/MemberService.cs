using Application.Bases.Exceptions;
using Application.Bases.Models;
using Application.Members;
using Domain.Groups;
using Domain.Members;
using Domain.Organizations;
using Domain.Policies;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.EntityFrameworkCore;

public class MemberService(AppDbContext dbContext) : IMemberService
{
    private IQueryable<TEntity> QueryableOf<TEntity>() where TEntity : class => dbContext.Set<TEntity>().AsQueryable();
    private DbSet<TEntity> SetOf<TEntity>() where TEntity : class => dbContext.Set<TEntity>();

    public async Task<Member> GetAsync(Guid organizationId, Guid memberId)
    {
        var users = QueryableOf<User>();
        var organizationUsers = QueryableOf<OrganizationUser>();

        var query =
            from user in users
            join organizationUser in organizationUsers
                on user.Id equals organizationUser.UserId
            where organizationUser.OrganizationId == organizationId && user.Id == memberId
            select new Member
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                InvitorId = organizationUser.InvitorId,
                InitialPassword = organizationUser.InitialPassword,
            };

        var member = await query.FirstOrDefaultAsync();
        if (member == null)
        {
            throw new EntityNotFoundException(nameof(Member), $"{organizationId}-{memberId}");
        }

        // get member groups
        member.Groups = await GetGroupsAsync(organizationId, new[] { member.Id });

        return member;
    }

    public async Task DeleteAsync(Guid organizationId, Guid memberId)
    {
        // delete organization user
        await QueryableOf<OrganizationUser>()
            .Where(x => x.OrganizationId == organizationId && x.UserId == memberId)
            .ExecuteDeleteAsync();

        // delete group member
        await QueryableOf<GroupMember>()
            .Where(x => x.OrganizationId == organizationId && x.MemberId == memberId)
            .ExecuteDeleteAsync();

        // delete member policies
        await QueryableOf<MemberPolicy>()
            .Where(x => x.OrganizationId == organizationId && x.MemberId == memberId)
            .ExecuteDeleteAsync();
    }

    public async Task<PagedResult<Member>> GetListAsync(Guid organizationId, MemberFilter filter)
    {
        var users = QueryableOf<User>();
        var organizationUsers = QueryableOf<OrganizationUser>();

        var query =
            from user in users
            join organizationUser in organizationUsers
                on user.Id equals organizationUser.UserId
            where organizationUser.OrganizationId == organizationId
            select new Member
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                InvitorId = organizationUser.InvitorId,
                InitialPassword = organizationUser.InitialPassword,
            };

        // email/name filter
        var searchText = filter.SearchText?.ToLower();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(x => x.Email.ToLower().Contains(searchText) || x.Name.ToLower().Contains(searchText));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip(filter.PageIndex * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        // get member groups
        var memberIds = items.Select(x => x.Id);
        var groups = await GetGroupsAsync(organizationId, memberIds);
        foreach (var member in items)
        {
            member.Groups = groups.Where(x => x.MemberId == member.Id);
        }

        return new PagedResult<Member>(totalCount, items);
    }

    public async Task<List<MemberGroup>> GetGroupsAsync(Guid organizationId, IEnumerable<Guid> memberIds)
    {
        var groups = QueryableOf<Group>();
        var groupMembers = QueryableOf<GroupMember>();

        var query =
            from theGroup in groups
            join groupMember in groupMembers
                on theGroup.Id equals groupMember.GroupId
            where theGroup.OrganizationId == organizationId && memberIds.Contains(groupMember.MemberId)
            select new MemberGroup
            {
                Id = theGroup.Id,
                Name = theGroup.Name,
                Description = theGroup.Description,
                MemberId = groupMember.MemberId,
                IsGroupMember = true
            };

        var items = await query.ToListAsync();
        return items;
    }

    public async Task<PagedResult<MemberGroup>> GetGroupsAsync(
        Guid organizationId,
        Guid memberId,
        MemberGroupFilter filter)
    {
        var groups = QueryableOf<Group>();
        var groupMembers = QueryableOf<GroupMember>();

        // aws document db not support '$let' operator which means we cannot operate on 'allGroupMembers' in the main query
        var query =
            from theGroup in groups
            join groupMember in groupMembers
                on theGroup.Id equals groupMember.GroupId
                into allGroupMembers
            where theGroup.OrganizationId == organizationId
            select new
            {
                theGroup.Id,
                theGroup.Name,
                theGroup.Description,
                GroupMembers = allGroupMembers
            };

        if (!filter.GetAllGroups)
        {
            query = query.Where(x => x.GroupMembers.Any(y => y.MemberId == memberId));
        }

        var name = filter.Name?.ToLower();
        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(x => x.Name.ToLower().Contains(name));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip(filter.PageIndex * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        var memberGroups = items.Select(x => new MemberGroup
        {
            Id = x.Id,
            Name = x.Name,
            Description = x.Description,
            MemberId = memberId,
            IsGroupMember = x.GroupMembers.Any(y => y.MemberId == memberId)
        }).ToList();

        return new PagedResult<MemberGroup>(totalCount, memberGroups);
    }

    public async Task<IEnumerable<Policy>> GetPoliciesAsync(Guid organizationId, Guid memberId)
    {
        // direct policies
        var policies = QueryableOf<Policy>();
        var memberPolicies = QueryableOf<MemberPolicy>();

        var directPolicyQuery =
            from policy in policies
            join memberPolicy in memberPolicies
                on policy.Id equals memberPolicy.PolicyId
            where memberPolicy.OrganizationId == organizationId && memberPolicy.MemberId == memberId
            select policy;

        // inherited policies
        var groups = QueryableOf<Group>();
        var groupMembers = QueryableOf<GroupMember>();
        var groupPolicies = QueryableOf<GroupPolicy>();

        var inheritedPolicyQuery =
            from theGroup in groups
            join groupMember in groupMembers
                on theGroup.Id equals groupMember.GroupId
            join groupPolicy in groupPolicies
                on theGroup.Id equals groupPolicy.GroupId
            join policy in policies
                on groupPolicy.PolicyId equals policy.Id
            where groupMember.OrganizationId == organizationId && groupMember.MemberId == memberId
            select policy;

        var directPolicies = await directPolicyQuery.ToListAsync();
        var inheritedPolicies = await inheritedPolicyQuery.ToListAsync();

        // distinct by policy name
        var allPolicies = directPolicies.Concat(inheritedPolicies).GroupBy(x => x.Name).Select(x => x.First());
        return allPolicies;
    }

    public async Task<PagedResult<MemberPolicyVm>> GetDirectPoliciesAsync(
        Guid organizationId,
        Guid memberId,
        MemberPolicyFilter filter)
    {
        var policies = QueryableOf<Policy>();
        var memberPolicies = QueryableOf<MemberPolicy>();

        var query =
            from policy in policies
            join memberPolicy in memberPolicies
                on policy.Id equals memberPolicy.PolicyId
                into allPolicyMembers
            where policy.OrganizationId == organizationId || policy.Type == PolicyTypes.SysManaged
            select new
            {
                policy.Id,
                policy.Name,
                policy.Type,
                policy.Description,
                AllPolicyMembers = allPolicyMembers
            };

        if (!filter.GetAllPolicies)
        {
            query = query.Where(
                x => x.AllPolicyMembers.Any(y => y.OrganizationId == organizationId && y.MemberId == memberId)
            );
        }

        // name filter
        var name = filter.Name?.ToLower();
        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(x => x.Name.ToLower().Contains(name));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip(filter.PageIndex * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        var vms = items.Select(x => new MemberPolicyVm
        {
            Id = x.Id.ToString(),
            Name = x.Name,
            Type = x.Type,
            Description = x.Description,
            IsMemberPolicy = x.AllPolicyMembers.Any(y => y.OrganizationId == organizationId && y.MemberId == memberId)
        }).ToList();

        return new PagedResult<MemberPolicyVm>(totalCount, vms);
    }

    public async Task<PagedResult<InheritedMemberPolicy>> GetInheritedPoliciesAsync(
        Guid organizationId,
        Guid memberId,
        InheritedMemberPolicyFilter filter)
    {
        var groups = QueryableOf<Group>();
        var groupMembers = QueryableOf<GroupMember>();
        var groupPolicies = QueryableOf<GroupPolicy>();
        var policies = QueryableOf<Policy>();

        var query =
            from theGroup in groups
            join groupMember in groupMembers
                on theGroup.Id equals groupMember.GroupId
            join groupPolicy in groupPolicies
                on theGroup.Id equals groupPolicy.GroupId
            join policy in policies
                on groupPolicy.PolicyId equals policy.Id
            where groupMember.OrganizationId == organizationId && groupMember.MemberId == memberId
            select new InheritedMemberPolicy
            {
                Id = policy.Id,
                Name = policy.Name,
                Description = policy.Description,
                Type = policy.Type,
                GroupName = theGroup.Name
            };

        var name = filter.Name?.ToLower();
        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(x => x.Name.ToLower().Contains(name));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip(filter.PageIndex * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new PagedResult<InheritedMemberPolicy>(totalCount, items);
    }

    public async Task AddPolicyAsync(MemberPolicy policy)
    {
        var existed = await QueryableOf<MemberPolicy>().CountAsync(
            x => x.OrganizationId == policy.OrganizationId && x.MemberId == policy.MemberId &&
                 x.PolicyId == policy.PolicyId
        ) > 0;
        if (existed)
        {
            return;
        }

        SetOf<MemberPolicy>().Add(policy);
        await dbContext.SaveChangesAsync();
    }

    public async Task RemovePolicyAsync(Guid organizationId, Guid memberId, Guid policyId)
    {
        await SetOf<MemberPolicy>()
            .Where(x => x.OrganizationId == organizationId && x.MemberId == memberId && x.PolicyId == policyId)
            .ExecuteDeleteAsync();
    }
}