using Microsoft.Extensions.Logging;
using Moq;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models.Signal.Accounts;
using SignalCli.Models.Signal.Groups;
using SignalCli.Services.Signal;

namespace SignalCli.Tests;

/// <summary>
/// C.5c (F5): facade-методи не повинні логувати PII (номери, UUID, назви груп, склад)
/// на рівні Information чи Debug. Деталі дозволені лише на Trace.
/// </summary>
public class PrivacyLoggingTests
{
    [Fact]
    public async Task ListAccounts_DoesNotLogPhoneNumber_AboveTrace()
    {
        // Підставляємо акаунт із номером телефону — він НЕ повинен з'являтися
        // в жодному Information/Warning/Error/Debug записі.
        var phoneNumber = "+380501234567";
        // post-modernize-tuning §4.20 (audit N10): wrapper-record замість List<T>-успадкування.
        var response = new ListAccountsResponse([new Account(phoneNumber)]);

        var client = new Mock<ISignalCliClient>();
        client.Setup(c => c.InvokeMethodAsync<ListAccountsParameters, ListAccountsResponse>(
                "listAccounts", It.IsAny<ListAccountsParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var logger = new Mock<ILogger<SignalAccounts>>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var sut = new SignalAccounts(client.Object, logger.Object);

        await sut.ListAccountsAsync();

        // Будь-який лог-запис рівня >= Debug із вмістом номера телефону — порушення приватності.
        foreach (var level in new[] { LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error })
        {
            logger.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(phoneNumber)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never,
                $"PII (номер телефону) не повинен потрапляти у лог рівня {level}");
        }
    }

    [Fact]
    public async Task ListGroups_DoesNotLogGroupId_AboveTrace()
    {
        // Готуємо «групу» з впізнаваним id; він не має з'являтися в Information.
        var groupId = "groupId-XYZ-abc";
        // post-modernize-tuning §4.20 (audit N10): wrapper-record (Items: IReadOnlyList<Group>).
        var response = new ListGroupsResponse([
            new Group(
                Id: groupId,
                Name: "GroupName",
                Description: null,
                IsMember: true,
                IsBlocked: false,
                MessageExpirationTime: 0,
                Members: [],
                PendingMembers: [],
                RequestingMembers: [],
                Admins: [],
                Banned: [],
                PermissionAddMember: "EVERY_MEMBER",
                PermissionEditDetails: "EVERY_MEMBER",
                PermissionSendMessage: "EVERY_MEMBER",
                GroupInviteLink: null),
        ]);

        var client = new Mock<ISignalCliClient>();
        client.Setup(c => c.InvokeMethodAsync<ListGroupsParameters, ListGroupsResponse>(
                "listGroups", It.IsAny<ListGroupsParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var logger = new Mock<ILogger<SignalGroups>>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var sut = new SignalGroups(client.Object, logger.Object);

        await sut.ListGroupsAsync("+1");

        foreach (var level in new[] { LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error })
        {
            logger.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(groupId)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never,
                $"PII (group id) не повинен потрапляти у лог рівня {level}");
        }
    }
}
