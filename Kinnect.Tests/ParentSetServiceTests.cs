using Kinnect.Services.Abstractions;
using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class ParentSetServiceTests
{
    private static readonly TestUserContext Editor = new("editor", isEditor: true);
    private static readonly TestUserContext Member = new("member");

    private static async Task<Person> AddPersonAsync(DbContextOptions<ApplicationDbContext> options, string name, bool? male, string? userId = null)
    {
        await using var db = SqliteDb.CreateContext(options);
        var person = new Person
        {
            FamilyName = "Ward",
            GivenNames = name,
            IsMale = male,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person;
    }

    private static async Task AddBirthAsync(DbContextOptions<ApplicationDbContext> options, Person person, short year)
    {
        await using var db = SqliteDb.CreateContext(options);
        db.PersonEvents.Add(new PersonEvent { PersonId = person.Id, EventType = PersonEventType.Birth, Year = year, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AddAsync_FirstSetIsPrimary_LaterSetsAreNot()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var birthFather = await AddPersonAsync(options, "Adam", true);
        var adopter = await AddPersonAsync(options, "Sam", true);
        var sut = TestParentSets.Create(factory);

        var first = await sut.AddAsync(child.Id, new ParentSetRequest { Type = ParentageType.Birth, FatherId = birthFather.Id });
        var second = await sut.AddAsync(child.Id, new ParentSetRequest { Type = ParentageType.Adopted, FatherId = adopter.Id, Year = 2001 });

        Assert.True(first.Value.IsPrimary);
        Assert.False(second.Value.IsPrimary);
        Assert.Equal((short?)2001, second.Value.Year);
    }

    [Fact]
    public async Task AddAsync_MakePrimary_MovesThePrimarySet()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var birthFather = await AddPersonAsync(options, "Adam", true);
        var adopter = await AddPersonAsync(options, "Sam", true);
        var sut = TestParentSets.Create(factory);
        await sut.AddAsync(child.Id, new ParentSetRequest { FatherId = birthFather.Id });

        await sut.AddAsync(child.Id, new ParentSetRequest { Type = ParentageType.Adopted, FatherId = adopter.Id, MakePrimary = true });

        var sets = await sut.GetForChildAsync(child.Id);
        Assert.Equal(2, sets.Count);
        Assert.Equal(ParentageType.Adopted, Assert.Single(sets, set => set.IsPrimary).Type);
    }

    [Fact]
    public async Task AddAsync_BirthSetKeepsNoDate()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var father = await AddPersonAsync(options, "Adam", true);

        var result = await TestParentSets.Create(factory).AddAsync(child.Id, new ParentSetRequest { FatherId = father.Id, Year = 1990 });

        Assert.Null(result.Value.Year);
    }

    [Fact]
    public async Task AddAsync_SetsRolesFromSex()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var mum = await AddPersonAsync(options, "Eve", false);
        var dad = await AddPersonAsync(options, "Adam", true);

        // Asked in the "wrong" order on purpose.
        var result = await TestParentSets.Create(factory).AddAsync(child.Id, new ParentSetRequest { FatherId = mum.Id, MotherId = dad.Id });

        Assert.Equal(dad.Id, result.Value.FatherId);
        Assert.Equal(mum.Id, result.Value.MotherId);
    }

    [Fact]
    public async Task AddAsync_ParentIsChild_ReturnsInvalid()
    {
        var (options, factory) = SqliteDb.Create();
        var person = await AddPersonAsync(options, "Jane", false);

        var result = await TestParentSets.Create(factory).AddAsync(person.Id, new ParentSetRequest { FatherId = person.Id });

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task AddAsync_WouldMakeSomeoneTheirOwnAncestor_ReturnsInvalid()
    {
        var (options, factory) = SqliteDb.Create();
        var grandchild = await AddPersonAsync(options, "Zed", true);
        var parent = await AddPersonAsync(options, "Pat", false);
        var grandparent = await AddPersonAsync(options, "Gus", true);
        var sut = TestParentSets.Create(factory);
        Assert.True((await sut.AddAsync(grandchild.Id, new ParentSetRequest { MotherId = parent.Id })).IsSuccess);
        Assert.True((await sut.AddAsync(parent.Id, new ParentSetRequest { FatherId = grandparent.Id })).IsSuccess);

        // Adopted or not, a loop through any set is refused.
        var result = await sut.AddAsync(grandparent.Id, new ParentSetRequest { Type = ParentageType.Foster, FatherId = grandchild.Id });

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task AddAsync_ChildBornBeforeParent_ReturnsInvalid()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var adopter = await AddPersonAsync(options, "Sam", true);
        await AddBirthAsync(options, child, 1990);
        await AddBirthAsync(options, adopter, 2000);

        var result = await TestParentSets.Create(factory).AddAsync(child.Id, new ParentSetRequest { Type = ParentageType.Adopted, FatherId = adopter.Id });

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task AddAsync_AdoptionDatedBeforeBirth_ReturnsInvalid()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var adopter = await AddPersonAsync(options, "Sam", true);
        await AddBirthAsync(options, child, 1990);

        var result = await TestParentSets.Create(factory).AddAsync(child.Id, new ParentSetRequest { Type = ParentageType.Adopted, FatherId = adopter.Id, Year = 1980 });

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task AddAsync_SameParentsTwice_ReturnsConflict()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var father = await AddPersonAsync(options, "Adam", true);
        var sut = TestParentSets.Create(factory);
        await sut.AddAsync(child.Id, new ParentSetRequest { FatherId = father.Id });

        var result = await sut.AddAsync(child.Id, new ParentSetRequest { FatherId = father.Id });

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task AddAsync_ByAnotherMember_IsForbidden()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var father = await AddPersonAsync(options, "Adam", true);

        var result = await TestParentSets.Create(factory, Member).AddAsync(child.Id, new ParentSetRequest { FatherId = father.Id });

        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task AddAsync_ByEditorWithLimitedVisibility_IsForbidden()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var father = await AddPersonAsync(options, "Adam", true);

        var result = await TestParentSets.Create(factory, Editor).AddAsync(child.Id, new ParentSetRequest { FatherId = father.Id, Visibility = LinkVisibility.FamilyAdmins });

        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task HiddenSet_IsNotSeenByAMember_AndCannotBeChangedByThem()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var birthFather = await AddPersonAsync(options, "Adam", true);
        var adopter = await AddPersonAsync(options, "Sam", true);
        await using var db = SqliteDb.CreateContext(options);
        await TestParentSets.LinkAsync(db, child, birthFather, isPrimary: true);
        var secret = await TestParentSets.LinkAsync(db, child, adopter, type: ParentageType.Adopted, isPrimary: false, visibility: LinkVisibility.FamilyAdmins);

        var asMember = TestParentSets.Create(factory, Member);
        var asAdmin = TestParentSets.Create(factory);

        Assert.Single(await asMember.GetForChildAsync(child.Id));
        Assert.Equal(2, (await asAdmin.GetForChildAsync(child.Id)).Count);
        Assert.Equal(ResultStatus.NotFound, (await asMember.RemoveAsync(secret.Id)).Status);
        Assert.Equal(ResultStatus.NotFound, (await asMember.UpdateAsync(secret.Id, new ParentSetRequest { Type = ParentageType.Adopted, FatherId = adopter.Id })).Status);
        Assert.Equal(ResultStatus.NotFound, (await asMember.AddParentAsync(secret.Id, birthFather.Id)).Status);
        Assert.Equal(2, (await asAdmin.GetForChildAsync(child.Id)).Count);
    }

    [Fact]
    public async Task HiddenDuplicate_IsRefusedWithoutSayingWhy()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false, userId: "jane");
        var adopter = await AddPersonAsync(options, "Sam", true);
        await using var db = SqliteDb.CreateContext(options);
        await TestParentSets.LinkAsync(db, child, adopter, type: ParentageType.Adopted, visibility: LinkVisibility.ChildOnly);

        var result = await TestParentSets.Create(factory, Editor).AddAsync(child.Id, new ParentSetRequest { Type = ParentageType.Adopted, FatherId = adopter.Id });

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("adopt", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChildOnly_IsSeenByTheChildAndNobodyElse_NotEvenAnAdministrator()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false, userId: "jane");
        var adopter = await AddPersonAsync(options, "Sam", true);
        await using var db = SqliteDb.CreateContext(options);
        await TestParentSets.LinkAsync(db, child, adopter, type: ParentageType.Adopted, visibility: LinkVisibility.ChildOnly);

        Assert.Single(await TestParentSets.Create(factory, new TestUserContext("jane")).GetForChildAsync(child.Id));
        Assert.Empty(await TestParentSets.Create(factory, TestUserContext.Admin).GetForChildAsync(child.Id));
        Assert.Empty(await TestParentSets.Create(factory, Member).GetForChildAsync(child.Id));
        Assert.Empty(await TestParentSets.Create(factory, new TestUserContext()).GetForChildAsync(child.Id));
    }

    [Fact]
    public async Task TheChildCanLimitWhoSeesASet()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false, userId: "jane");
        var adopter = await AddPersonAsync(options, "Sam", true);
        var asChild = TestParentSets.Create(factory, new TestUserContext("jane"));
        var added = await asChild.AddAsync(child.Id, new ParentSetRequest { Type = ParentageType.Adopted, FatherId = adopter.Id });

        var limited = await asChild.UpdateAsync(added.Value.Id, new ParentSetRequest { Type = ParentageType.Adopted, FatherId = adopter.Id, Visibility = LinkVisibility.ChildOnly });

        Assert.True(limited.IsSuccess);
        Assert.Equal(LinkVisibility.ChildOnly, limited.Value.Visibility);
        Assert.Empty(await TestParentSets.Create(factory, Member).GetForChildAsync(child.Id));
    }

    [Fact]
    public async Task EditorCannotChangeVisibilityOfSomeoneElsesSet()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var father = await AddPersonAsync(options, "Adam", true);
        var set = await TestParentSets.Create(factory).AddAsync(child.Id, new ParentSetRequest { FatherId = father.Id });

        var result = await TestParentSets.Create(factory, Editor).UpdateAsync(set.Value.Id, new ParentSetRequest { FatherId = father.Id, Visibility = LinkVisibility.FamilyAdmins });

        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task HiddenPrimary_IsReplacedByTheVisibleSet_WithoutChangingStoredData()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var birthFather = await AddPersonAsync(options, "Adam", true);
        var adopter = await AddPersonAsync(options, "Sam", true);
        await using var db = SqliteDb.CreateContext(options);
        var hiddenPrimary = await TestParentSets.LinkAsync(db, child, adopter, type: ParentageType.Adopted, isPrimary: true, visibility: LinkVisibility.FamilyAdmins);
        await TestParentSets.LinkAsync(db, child, birthFather, isPrimary: false);

        var seen = await TestParentSets.Create(factory, Member).GetForChildAsync(child.Id);

        Assert.True(Assert.Single(seen).IsPrimary);
        await using var check = SqliteDb.CreateContext(options);
        Assert.True((await check.ParentSets.SingleAsync(set => set.Id == hiddenPrimary.Id)).IsPrimary);
    }

    [Fact]
    public async Task RemoveAsync_RemovingThePrimary_PromotesTheDefaultNext()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var birthFather = await AddPersonAsync(options, "Adam", true);
        var foster = await AddPersonAsync(options, "Fay", false);
        var sut = TestParentSets.Create(factory);
        var birth = await sut.AddAsync(child.Id, new ParentSetRequest { FatherId = birthFather.Id });
        await sut.AddAsync(child.Id, new ParentSetRequest { Type = ParentageType.Foster, MotherId = foster.Id });

        await sut.RemoveAsync(birth.Value.Id);

        var left = Assert.Single(await sut.GetForChildAsync(child.Id));
        Assert.True(left.IsPrimary);
        Assert.Equal(ParentageType.Foster, left.Type);
    }

    [Fact]
    public async Task RemoveParentAsync_LastParent_RemovesTheSet()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var father = await AddPersonAsync(options, "Adam", true);
        var sut = TestParentSets.Create(factory);
        var set = await sut.AddAsync(child.Id, new ParentSetRequest { FatherId = father.Id });

        await sut.RemoveParentAsync(set.Value.Id, father.Id);

        Assert.Empty(await sut.GetForChildAsync(child.Id));
    }

    [Fact]
    public async Task DetachParentAsync_DropsTheParentAndEmptySets()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var father = await AddPersonAsync(options, "Adam", true);
        var mother = await AddPersonAsync(options, "Eve", false);
        var adopter = await AddPersonAsync(options, "Sam", true);
        var sut = TestParentSets.Create(factory);
        await sut.AddAsync(child.Id, new ParentSetRequest { FatherId = father.Id, MotherId = mother.Id });
        await sut.AddAsync(child.Id, new ParentSetRequest { Type = ParentageType.Adopted, FatherId = adopter.Id, MakePrimary = true });

        await sut.DetachParentAsync(adopter.Id);

        var sets = await sut.GetForChildAsync(child.Id);
        var birth = Assert.Single(sets);
        Assert.True(birth.IsPrimary);
        Assert.Equal(2, birth.Members.Count);
    }

    [Fact]
    public async Task SetBirthParentsAsync_AddsReplacesAndClearsTheBirthSet()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var father = await AddPersonAsync(options, "Adam", true);
        var mother = await AddPersonAsync(options, "Eve", false);
        var adopter = await AddPersonAsync(options, "Sam", true);
        var sut = TestParentSets.Create(factory);
        await sut.AddAsync(child.Id, new ParentSetRequest { Type = ParentageType.Adopted, FatherId = adopter.Id });

        await sut.SetBirthParentsAsync(child.Id, father.Id, null);
        await sut.SetBirthParentsAsync(child.Id, father.Id, mother.Id);

        var birth = Assert.Single(await sut.GetForChildAsync(child.Id), set => set.Type == ParentageType.Birth);
        Assert.Equal(mother.Id, birth.MotherId);
        // The adoption is untouched.
        Assert.Contains(await sut.GetForChildAsync(child.Id), set => set.Type == ParentageType.Adopted);

        await sut.SetBirthParentsAsync(child.Id, null, null);

        Assert.DoesNotContain(await sut.GetForChildAsync(child.Id), set => set.Type == ParentageType.Birth);
    }

    [Fact]
    public async Task SyncAsync_LeavesHiddenSetsAloneAndKeepsTheirPrimary()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var birthFather = await AddPersonAsync(options, "Adam", true);
        var adopter = await AddPersonAsync(options, "Sam", true);
        await using var db = SqliteDb.CreateContext(options);
        var hidden = await TestParentSets.LinkAsync(db, child, adopter, type: ParentageType.Adopted, isPrimary: true, visibility: LinkVisibility.FamilyAdmins);
        var visible = await TestParentSets.LinkAsync(db, child, birthFather, isPrimary: false);
        var asMember = TestParentSets.Create(factory, Member);

        // The component only knows the visible set, shown as primary. Sending it back unchanged changes nothing.
        var result = await asMember.SyncAsync(child.Id, [new ParentSetSync(visible.Id.ToString(), ParentageType.Birth, [birthFather.Id], null, null, null, true)]);

        Assert.True(result.IsSuccess);
        var asAdmin = TestParentSets.Create(factory);
        await using var check = SqliteDb.CreateContext(options);
        Assert.True((await check.ParentSets.SingleAsync(set => set.Id == hidden.Id)).IsPrimary);
        Assert.Equal(2, (await asAdmin.GetForChildAsync(child.Id)).Count);
    }

    [Fact]
    public async Task SyncAsync_AnAdministratorEditingTheirView_DoesNotTouchSetsTheyCannotSee()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false, userId: "jane");
        var birthFather = await AddPersonAsync(options, "Adam", true);
        var adopter = await AddPersonAsync(options, "Sam", true);
        var foster = await AddPersonAsync(options, "Fay", false);
        await using var db = SqliteDb.CreateContext(options);
        var hidden = await TestParentSets.LinkAsync(db, child, adopter, type: ParentageType.Adopted, isPrimary: true, visibility: LinkVisibility.ChildOnly);
        var visible = await TestParentSets.LinkAsync(db, child, birthFather, isPrimary: false);

        var result = await TestParentSets.Create(factory).SyncAsync(child.Id,
        [
            new ParentSetSync(visible.Id.ToString(), ParentageType.Birth, [birthFather.Id], null, null, null, true),
            new ParentSetSync("new", ParentageType.Foster, [foster.Id], null, null, null, false),
        ]);

        Assert.True(result.IsSuccess);
        await using var check = SqliteDb.CreateContext(options);
        Assert.Equal(3, await check.ParentSets.CountAsync());
        var stillHidden = await check.ParentSets.SingleAsync(set => set.Id == hidden.Id);
        Assert.True(stillHidden.IsPrimary);
        Assert.Equal(LinkVisibility.ChildOnly, stillHidden.Visibility);
        Assert.Equal(1, await check.ParentSets.CountAsync(set => set.IsPrimary));
    }

    [Fact]
    public async Task SyncAsync_RemovesSetsThatLeftTheView_AndChangesTheViewersPrimary()
    {
        var (options, factory) = SqliteDb.Create();
        var child = await AddPersonAsync(options, "Jane", false);
        var birthFather = await AddPersonAsync(options, "Adam", true);
        var adopter = await AddPersonAsync(options, "Sam", true);
        var foster = await AddPersonAsync(options, "Fay", false);
        await using var db = SqliteDb.CreateContext(options);
        var birth = await TestParentSets.LinkAsync(db, child, birthFather, isPrimary: true);
        var adopted = await TestParentSets.LinkAsync(db, child, adopter, type: ParentageType.Adopted, isPrimary: false);
        var fostered = await TestParentSets.LinkAsync(db, child, foster, type: ParentageType.Foster, isPrimary: false);
        var sut = TestParentSets.Create(factory);

        var result = await sut.SyncAsync(child.Id,
        [
            new ParentSetSync(birth.Id.ToString(), ParentageType.Birth, [birthFather.Id], null, null, null, false),
            new ParentSetSync(adopted.Id.ToString(), ParentageType.Adopted, [adopter.Id], 2001, null, null, true),
        ]);

        Assert.True(result.IsSuccess);
        var sets = await sut.GetForChildAsync(child.Id);
        Assert.Equal(2, sets.Count);
        Assert.DoesNotContain(sets, set => set.Id == fostered.Id);
        var primary = Assert.Single(sets, set => set.IsPrimary);
        Assert.Equal(adopted.Id, primary.Id);
        Assert.Equal((short?)2001, primary.Year);
    }
}
