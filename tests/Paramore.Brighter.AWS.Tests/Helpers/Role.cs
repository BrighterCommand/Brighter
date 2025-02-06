using System;
using System.Net;
using System.Threading.Tasks;
using Amazon.IdentityManagement.Model;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests.Helpers;

public static class Role
{
    public static async Task<string> GetOrCreateRoleAsync(AWSClientFactory factory, string roleName)
    {
        using var client = factory.CreateIdentityClient();
        try
        {
            var role = await client.GetRoleAsync(new GetRoleRequest { RoleName = roleName });
            if (role.HttpStatusCode == HttpStatusCode.OK)
            {
                return role.Role.Arn;
            }
        }
        catch 
        {
            // We are going to create case do not exists
        }

        var createRoleResponse = await client.CreateRoleAsync(new CreateRoleRequest
        {
            RoleName = roleName,
            AssumeRolePolicyDocument = """
                                       {
                                            "Version": "2012-10-17",
                                            "Statement": [
                                            {
                                                "Effect": "Allow",
                                                "Principal": {
                                                    "Service": "scheduler.amazonaws.com"
                                                },
                                                "Action": "sts:AssumeRole"
                                             },
                                             {
                                                "Effect": "Allow",
                                                "Resource": "*",
                                                "Action": "*"
                                             }]
                                       }
                                       """
        });
        return createRoleResponse.Role.Arn;
    }
}
