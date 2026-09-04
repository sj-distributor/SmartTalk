using Shouldly;
using SmartTalk.Core.Utils;
using Xunit;

namespace SmartTalk.UnitTests.Utils;

public class CustomerOrderUnitClassifierTests
{
    [Theory]
    [InlineData("箱")]
    [InlineData("case")]
    [InlineData("CS")]
    public void Classify_ShouldReturnCaseForCaseUnits(string unit)
    {
        CustomerOrderUnitClassifier.Classify(unit).ShouldBe(CustomerOrderUnitClassifier.Case);
        CustomerOrderUnitClassifier.IsCase(unit).ShouldBeTrue();
        CustomerOrderUnitClassifier.GetPreferredMaterialUnit(unit).ShouldBe(CustomerOrderUnitClassifier.Case);
    }

    [Theory]
    [InlineData("PC")]
    [InlineData("包")]
    [InlineData("件")]
    [InlineData("盒")]
    [InlineData("扎")]
    [InlineData("tray")]
    public void Classify_ShouldReturnPieceForNonCaseNonPoundUnits(string unit)
    {
        CustomerOrderUnitClassifier.Classify(unit).ShouldBe(CustomerOrderUnitClassifier.Piece);
        CustomerOrderUnitClassifier.IsCase(unit).ShouldBeFalse();
        CustomerOrderUnitClassifier.GetPreferredMaterialUnit(unit).ShouldBe(CustomerOrderUnitClassifier.Piece);
    }

    [Theory]
    [InlineData("磅")]
    [InlineData("lb")]
    [InlineData("lbs")]
    [InlineData("pound")]
    public void Classify_ShouldReturnPoundWithoutMaterialUnitPreference(string unit)
    {
        CustomerOrderUnitClassifier.Classify(unit).ShouldBe(CustomerOrderUnitClassifier.Pound);
        CustomerOrderUnitClassifier.GetPreferredMaterialUnit(unit).ShouldBeEmpty();
    }
}
