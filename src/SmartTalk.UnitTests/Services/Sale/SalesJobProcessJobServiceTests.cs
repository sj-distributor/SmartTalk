using System.Linq.Expressions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Settings.Jobs;
using Xunit;

namespace SmartTalk.UnitTests.Services.Sale;

public class SalesJobProcessJobServiceTests
{
    private readonly ICrmClient _crmClient = Substitute.For<ICrmClient>();
    private readonly ISalesService _salesService = Substitute.For<ISalesService>();
    private readonly ISalesDataProvider _salesDataProvider = Substitute.For<ISalesDataProvider>();
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient = Substitute.For<ISmartTalkBackgroundJobClient>();

    [Fact]
    public async Task ScheduleRefreshCustomerItemsCacheAsync_ShouldEnqueueCustomerIdsInBatchesOfTen()
    {
        var capturedBatches = new List<List<string>>();
        _salesDataProvider.GetAllSalesAsync(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 21)
                .Select(x => new Sales { Name = x.ToString("00000") })
                .Append(new Sales { Name = "00001/00022" })
                .ToList());

        _backgroundJobClient.Enqueue(
                Arg.Do<Expression<Func<ISalesJobProcessJobService, Task>>>(expression =>
                {
                    var methodCall = expression.Body as MethodCallExpression;
                    methodCall.ShouldNotBeNull();
                    var argument = methodCall.Arguments[0];
                    var batch = Expression.Lambda<Func<List<string>>>(argument).Compile().Invoke();
                    capturedBatches.Add(batch);
                }),
                HangfireConstants.InternalHostingCaCheKnowledgeVariable)
            .Returns("job-id");

        var sut = BuildService();

        await sut.ScheduleRefreshCustomerItemsCacheAsync(new(), CancellationToken.None);

        capturedBatches.Count.ShouldBe(3);
        capturedBatches[0].ShouldBe(Enumerable.Range(1, 10).Select(x => x.ToString("00000")).ToList());
        capturedBatches[1].ShouldBe(Enumerable.Range(11, 10).Select(x => x.ToString("00000")).ToList());
        capturedBatches[2].ShouldBe(["00021", "00022"]);
    }

    [Fact]
    public async Task ScheduleRefreshCustomerItemsCacheAsync_ShouldUseConfiguredBatchSize()
    {
        var capturedBatches = new List<List<string>>();
        _salesDataProvider.GetAllSalesAsync(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 15)
                .Select(x => new Sales { Name = x.ToString("00000") })
                .ToList());

        _backgroundJobClient.Enqueue(
                Arg.Do<Expression<Func<ISalesJobProcessJobService, Task>>>(expression =>
                {
                    var methodCall = expression.Body as MethodCallExpression;
                    methodCall.ShouldNotBeNull();
                    var argument = methodCall.Arguments[0];
                    var batch = Expression.Lambda<Func<List<string>>>(argument).Compile().Invoke();
                    capturedBatches.Add(batch);
                }),
                HangfireConstants.InternalHostingCaCheKnowledgeVariable)
            .Returns("job-id");

        var sut = BuildService(batchSize: 7);

        await sut.ScheduleRefreshCustomerItemsCacheAsync(new(), CancellationToken.None);

        capturedBatches.Count.ShouldBe(3);
        capturedBatches[0].Count.ShouldBe(7);
        capturedBatches[1].Count.ShouldBe(7);
        capturedBatches[2].ShouldBe(["00015"]);
    }

    [Fact]
    public async Task RefreshCustomerItemsCacheBySoldToIdsAsync_ShouldBuildOnceAndUpsertCustomerCachesInBatch()
    {
        _salesService.BuildCustomerItemsStringsAsync(
                Arg.Is<List<string>>(x => x.SequenceEqual(new[] { "00001", "00002" })),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>
            {
                ["00001"] = "items-1",
                ["00002"] = "items-2"
            });

        var sut = BuildService();

        await sut.RefreshCustomerItemsCacheBySoldToIdsAsync(["00001", "00002"], CancellationToken.None);

        await _salesService.Received(1).BuildCustomerItemsStringsAsync(
            Arg.Is<List<string>>(x => x.SequenceEqual(new[] { "00001", "00002" })),
            Arg.Any<CancellationToken>());

        await _salesDataProvider.Received(1).UpsertCustomerItemsCachesAsync(
            Arg.Is<Dictionary<string, string>>(x =>
                x.Count == 2 &&
                x["00001"] == "items-1" &&
                x["00002"] == "items-2"),
            Arg.Any<CancellationToken>());
    }

    private SalesJobProcessJobService BuildService(int batchSize = 10)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CustomerItemsRefreshBatchSize"] = batchSize.ToString()
            })
            .Build();

        return new SalesJobProcessJobService(
            _crmClient,
            _salesService,
            _salesDataProvider,
            _backgroundJobClient,
            new CustomerItemsRefreshBatchSizeSetting(configuration));
    }
}
