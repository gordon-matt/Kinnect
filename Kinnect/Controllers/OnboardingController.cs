using Kinnect.Data;

namespace Kinnect.Controllers;

[Authorize]
public class OnboardingController(
    ApplicationDbContext context,
    IUserContextService userContextService,
    IGedcomService gedcomService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        string? userId = userContextService.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        if (await context.People.AnyAsync(p => p.UserId == userId))
        {
            return RedirectToAction("Index", "Home");
        }

        bool hasAnyPeople = await context.People.AnyAsync();
        var model = new OnboardingViewModel
        {
            CanImportGedcom = User.IsInRole(Constants.Roles.Administrator) && !hasAnyPeople,
            ShowImportedPersonSelection = hasAnyPeople,
            People = hasAnyPeople
                ? await context.People
                    .Where(p => p.UserId == null)
                    .OrderBy(p => p.FamilyName)
                    .ThenBy(p => p.GivenNames)
                    .Select(p => new OnboardingPersonOption
                    {
                        Id = p.Id,
                        DisplayName = string.IsNullOrWhiteSpace(p.FamilyName)
                            ? p.GivenNames
                            : $"{p.GivenNames} {p.FamilyName}"
                    })
                    .ToListAsync()
                : []
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportGedcom(IFormFile? file)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        if (!User.IsInRole(Constants.Roles.Administrator))
        {
            return Forbid();
        }

        if (await context.People.AnyAsync())
        {
            TempData["OnboardingError"] = "GEDCOM import is only available once during first-time setup.";
            return RedirectToAction(nameof(Index));
        }

        if (file is null || file.Length == 0)
        {
            TempData["OnboardingError"] = "Please select a GEDCOM file to import.";
            return RedirectToAction(nameof(Index));
        }

        await using var stream = file.OpenReadStream();
        var result = await gedcomService.ImportAsync(stream);
        if (!result.IsSuccess)
        {
            TempData["OnboardingError"] = string.Join("; ", result.Errors);
        }
        else
        {
            TempData["OnboardingSuccess"] = "Import complete. Please select your name from the imported people.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkImportedPerson(int personId)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        if (await context.People.AnyAsync(p => p.UserId == userId))
        {
            return RedirectToAction("Index", "Home");
        }

        var person = await context.People.FirstOrDefaultAsync(p => p.Id == personId);
        if (person is null || !string.IsNullOrWhiteSpace(person.UserId))
        {
            TempData["OnboardingError"] = "That person cannot be linked. Please select an unlinked person.";
            return RedirectToAction(nameof(Index));
        }

        person.UserId = userId;
        person.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return RedirectToAction("Index", "FamilyTree");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateManualPerson(string givenNames, string familyName, bool isMale = true)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        if (await context.People.AnyAsync(p => p.UserId == userId))
        {
            return RedirectToAction("Index", "Home");
        }

        givenNames = (givenNames ?? string.Empty).Trim();
        familyName = (familyName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(givenNames))
        {
            TempData["OnboardingError"] = "Please enter your given name(s).";
            return RedirectToAction(nameof(Index));
        }

        await context.People.AddAsync(new Person
        {
            UserId = userId,
            FamilyName = familyName,
            GivenNames = givenNames,
            IsMale = isMale,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return RedirectToAction("Index", "FamilyTree");
    }
}

public sealed class OnboardingViewModel
{
    public bool CanImportGedcom { get; init; }
    public bool ShowImportedPersonSelection { get; init; }
    public List<OnboardingPersonOption> People { get; init; } = [];
}

public sealed class OnboardingPersonOption
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}
