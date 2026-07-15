using System.Security.Claims;
using HermesNotifier.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HermesNotifier.Api.Auth;

public sealed class AdminUserRequirementHandler : AuthorizationHandler<AdminUserRequirement>
{
    private readonly ApplicationDbContext _context;

    public AdminUserRequirementHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminUserRequirement requirement)
    {
        var lineId = context.User.FindFirstValue("lineId")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(lineId))
        {
            return;
        }

        var isAdmin = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.LineId == lineId && u.IsAdmin);

        if (isAdmin)
        {
            context.Succeed(requirement);
        }
    }
}
