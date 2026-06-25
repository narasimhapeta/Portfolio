using System.Security.Claims;
using AutoInsurance.Gateway.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace AutoInsurance.Gateway.Tests;

public class MockAuthMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SetsMockClaimsPrincipal_WithNameIdentifierClaim()
    {
        var context = new DefaultHttpContext();
        var nextWasCalled = false;
        RequestDelegate next = ctx =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new MockAuthMiddleware(next);
        await middleware.InvokeAsync(context);

        nextWasCalled.Should().BeTrue();
        context.User.Identity!.IsAuthenticated.Should().BeTrue();
        context.User.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("dev-user-001");
    }

    [Fact]
    public async Task InvokeAsync_SetsMockClaimsPrincipal_WithEmailClaim()
    {
        var context = new DefaultHttpContext();
        var middleware = new MockAuthMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.User.FindFirst(ClaimTypes.Email)!.Value.Should().Be("dev@autoinsurance.local");
    }

    [Fact]
    public async Task InvokeAsync_SetsMockClaimsPrincipal_WithB2CObjectId()
    {
        var context = new DefaultHttpContext();
        var middleware = new MockAuthMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")!.Value
            .Should().Be("dev-b2c-object-id-001");
    }
}
