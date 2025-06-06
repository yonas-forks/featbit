const dbName = "featbit";
print('use', dbName, 'database')
db = db.getSiblingDB(dbName)

print('seed started...')

// seed ids
const workspaceId = UUID()
const userId = UUID()
const organizationId = UUID()

// built-in policies
// see also: modules/back-end/src/Domain/Policies/BuiltInPolicy.cs
const ownerPolicyId = UUID("98881f6a-5c6c-4277-bcf7-fda94c538785")
const administratorPolicyId = UUID("3e961f0f-6fd4-4cf4-910f-52d356f8cc08")
const developerPolicyId = UUID("66f3687f-939d-4257-bd3f-c3553d39e1b6")

function getUUIDString() {
    return UUID().toString().split('"')[1];
}

// seed workspace
print('clean and seed collection: Workspaces')
db.Workspaces.deleteMany({})
db.Workspaces.insertOne(
    {
        _id: workspaceId,
        name: "Default Workspace",
        key: "default-workspace",
        sso: null,
        license: null,
        createdAt: new Date(),
        updatedAt: new Date()
    }
)
print('collection seeded: Workspaces')

// seed user
print('clean and seed collection: Users')
db.Users.deleteMany({})
db.Users.insertOne(
    {
        _id: userId,
        email: "test@featbit.com",
        password: "AQAAAAEAACcQAAAAELDHEjCrDQrmnAXU5C//mOLvUBJ7lnVFEMMFxNMDIIrF7xK8JDQKUifU3HH4gexNAQ==",
        name: "tester",
        origin: "Local",
        workspaceId: workspaceId,
        createAt: new Date(),
        updatedAt: new Date()
    }
)
print('collection seeded: Users')

// seed organization
print('clean and seed collection: Organizations')
db.Organizations.deleteMany({})
db.Organizations.insertOne(
    {
        _id: organizationId,
        workspaceId: workspaceId,
        name: "playground",
        key: "playground",
        initialized: false,
        createdAt: new Date(),
        updatedAt: new Date(),
        defaultPermissions: {
            policyIds: [developerPolicyId],
            groupIds: []
        }
    }
)
print('collection seeded: Organizations')

// seed organization users
print('clean and seed collection: OrganizationUsers')
db.OrganizationUsers.deleteMany({})
db.OrganizationUsers.insertOne(
    {
        _id: UUID(),
        organizationId: organizationId,
        userId: userId,
        invitorId: null,
        initialPassword: "",
        createdAt: new Date(),
        updatedAt: new Date()
    }
)
print('collection seeded: OrganizationUsers')

// seed system managed policies
print('clean and seed collection: Policies')
db.Policies.deleteMany({})
db.Policies.insertOne(
    {
        _id: ownerPolicyId,
        organizationId: null,
        name: "Owner",
        description: "Contains all permissions. This policy is granted to the user who created the organization",
        type: "SysManaged",
        statements: [
            {
                _id: getUUIDString(),
                resourceType: "*",
                effect: "allow",
                actions: ["*"],
                resources: ["*"]
            }
        ],
        createdAt: new Date(),
        updatedAt: new Date()
    }
)
db.Policies.insertOne(
    {
        _id: administratorPolicyId,
        organizationId: null,
        name: "Administrator",
        description: "Contains all the permissions required by an administrator",
        type: "SysManaged",
        statements: [
            {
                _id: getUUIDString(),
                resourceType: "organization",
                effect: "allow",
                actions: ["UpdateOrgName"],
                resources: ["organization/*"]
            },
            {
                _id: getUUIDString(),
                resourceType: "organization",
                effect: "allow",
                actions: ["UpdateOrgDefaultUserPermissions"],
                resources: ["organization/*"]
            },
            {
                _id: getUUIDString(),
                resourceType: "iam",
                effect: "allow",
                actions: ["CanManageIAM"],
                resources: ["iam/*"]
            },
            {
                _id: getUUIDString(),
                resourceType: "access-token",
                effect: "allow",
                actions: [
                    "ManageServiceAccessTokens",
                    "ManagePersonalAccessTokens",
                    "ListAccessTokens"
                ],
                resources: ["access-token/*"]
            },
            {
                _id: getUUIDString(),
                resourceType: "relay-proxy",
                effect: "allow",
                actions: [
                    "ManageRelayProxies",
                    "ListRelayProxies"
                ],
                resources: ["relay-proxy/*"]
            },
            {
                _id: getUUIDString(),
                resourceType: "project",
                effect: "allow",
                actions: [
                    "CanAccessProject",
                    "CreateProject",
                    "DeleteProject",
                    "UpdateProjectSettings",
                    "CreateEnv"
                ],
                resources: ["project/*"]
            },
            {
                _id: getUUIDString(),
                resourceType: "env",
                effect: "allow",
                actions: [
                    "DeleteEnv",
                    "UpdateEnvSettings",
                    "CreateEnvSecret",
                    "DeleteEnvSecret",
                    "UpdateEnvSecret"
                ],
                resources: ["project/*:env/*"]
            }
        ],
        createdAt: new Date(),
        updatedAt: new Date()
    }
)
db.Policies.insertOne(
    {
        _id: developerPolicyId,
        organizationId: null,
        name: "Developer",
        description: "Contains all the permissions required by a developer",
        type: "SysManaged",
        statements: [
            {
                _id: getUUIDString(),
                resourceType: "access-token",
                effect: "allow",
                actions: [
                    "ManageServiceAccessTokens",
                    "ManagePersonalAccessTokens",
                    "ListAccessTokens"
                ],
                resources: ["access-token/*"]
            },
            {
                _id: getUUIDString(),
                resourceType: "relay-proxy",
                effect: "allow",
                actions: [
                    "ManageRelayProxies",
                    "ListRelayProxies"
                ],
                resources: ["relay-proxy/*"]
            },
            {
                _id: getUUIDString(),
                resourceType: "project",
                effect: "allow",
                actions: [
                    "CanAccessProject"
                ],
                resources: ["project/*"]
            }
        ],
        createdAt: new Date(),
        updatedAt: new Date()
    }
)
print('collection seeded: Policies')

// seed member policy
print('clean and seed collection: MemberPolicies')
db.MemberPolicies.deleteMany({})
db.MemberPolicies.insertOne(
    {
        _id: UUID(),
        organizationId: organizationId,
        policyId: ownerPolicyId,
        memberId: userId,
        createdAt: new Date(),
        updatedAt: new Date()
    }
)
print('collection seeded: MemberPolicies')

// add indexes
print('add indexes...')
db.AuditLogs.createIndex({createdAt: 1});

db.EndUsers.createIndex({envId: 1, keyId: 1});
db.EndUsers.createIndex({updatedAt: 1});

db.ExperimentMetrics.createIndex({updatedAt: 1});
db.FeatureFlags.createIndex({updatedAt: 1});
db.Segments.createIndex({updatedAt: 1});
db.AccessTokens.createIndex({createdAt: 1});
db.Policies.createIndex({createdAt: 1});
db.Projects.createIndex({createdAt: 1});
db.RelayProxies.createIndex({createdAt: 1});
db.Webhooks.createIndex({createdAt: 1});
db.Webhooks.createIndex({startedAt: 1});
print('indexes added')
