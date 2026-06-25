using AutoInsurance.Shared;
using FluentAssertions;

namespace AutoInsurance.Shared.Tests;

public class ResultTests
{
    [Fact]
    public void GenericSuccess_SetsIsSuccessTrue_AndValue()
    {
        var result = Result<string>.Success("hello");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void GenericFailure_SetsIsSuccessFalse_AndError()
    {
        var result = Result<string>.Failure("something went wrong");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("something went wrong");
        result.Value.Should().BeNull();
    }

    [Fact]
    public void NonGenericSuccess_SetsIsSuccessTrue()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void NonGenericFailure_SetsIsSuccessFalse_AndError()
    {
        var result = Result.Failure("not found");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("not found");
    }
}
