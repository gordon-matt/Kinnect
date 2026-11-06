using Kinnect.Services;
using Kinnect.Services.Abstractions;
using Kinnect.Tests.Infrastructure;
using MyWebTree.Models;
using KinnectParentageType = Kinnect.Enums.ParentageType;
using WebParentageType = MyWebTree.ParentageType;

namespace Kinnect.Tests;

public class ParentGraphTests
{
    private static ParentSetView Set(int id, int child, KinnectParentageType type, bool primary, params (int Person, ParentRole Role)[] members) =>
        new(id, child, type, primary, null, null, null, LinkVisibility.Everyone, members.Select(member => new ParentMemberView(member.Person, member.Role)).ToList());

    [Fact]
    public void Build_WithoutAStoredPrimary_UsesAdoptedThenBirthThenFoster()
    {
        var graph = ParentGraph.Build(
        [
            Set(1, child: 10, KinnectParentageType.Foster, primary: false, (1, ParentRole.Father)),
            Set(2, child: 10, KinnectParentageType.Birth, primary: false, (2, ParentRole.Father)),
            Set(3, child: 10, KinnectParentageType.Adopted, primary: false, (3, ParentRole.Father)),
        ]);

        Assert.Equal(3, graph.PrimaryOf(10)!.Id);
        // The primary comes first, the rest keep the order they were recorded in.
        Assert.Equal([3, 1, 2], graph.SetsOf(10).Select(set => set.Id));
    }

    [Fact]
    public void Build_KeepsTheStoredPrimary_WhenItIsVisible()
    {
        var graph = ParentGraph.Build(
        [
            Set(1, child: 10, KinnectParentageType.Birth, primary: true, (1, ParentRole.Father)),
            Set(2, child: 10, KinnectParentageType.Adopted, primary: false, (2, ParentRole.Father)),
        ]);

        Assert.Equal(1, graph.PrimaryOf(10)!.Id);
    }

    [Fact]
    public void Build_ExactlyOneSetIsPrimary_EvenWhenTheDataSaysTwo()
    {
        var graph = ParentGraph.Build(
        [
            Set(1, child: 10, KinnectParentageType.Birth, primary: true, (1, ParentRole.Father)),
            Set(2, child: 10, KinnectParentageType.Adopted, primary: true, (2, ParentRole.Father)),
        ]);

        Assert.Single(graph.SetsOf(10), set => set.IsPrimary);
    }

    [Fact]
    public void Followed_ChoosesByLineage()
    {
        var graph = ParentGraph.Build(
        [
            Set(1, child: 10, KinnectParentageType.Birth, primary: false, (1, ParentRole.Father), (2, ParentRole.Mother)),
            Set(2, child: 10, KinnectParentageType.Adopted, primary: true, (3, ParentRole.Father)),
            Set(3, child: 10, KinnectParentageType.Foster, primary: false, (4, ParentRole.Mother)),
        ]);

        Assert.Equal([3], graph.ParentsOf(10));
        Assert.Equal([1, 2], graph.ParentsOf(10, ParentLineage.BirthOnly));
        Assert.Equal([3, 1, 2, 4], graph.ParentsOf(10, ParentLineage.Any));
    }

    [Fact]
    public void Siblings_FollowTheLineage()
    {
        // 10 and 11 share a birth father. 10 is also adopted into another family, and that is the one the tree shows.
        var graph = ParentGraph.Build(
        [
            Set(1, child: 10, KinnectParentageType.Birth, primary: false, (1, ParentRole.Father)),
            Set(2, child: 10, KinnectParentageType.Adopted, primary: true, (3, ParentRole.Father)),
            Set(3, child: 11, KinnectParentageType.Birth, primary: true, (1, ParentRole.Father)),
            Set(4, child: 12, KinnectParentageType.Birth, primary: true, (3, ParentRole.Father)),
        ]);

        Assert.Equal([12], graph.SiblingsOf(10));
        Assert.Equal([11], graph.SiblingsOf(10, ParentLineage.BirthOnly));
        Assert.Equal([11, 12], graph.SiblingsOf(10, ParentLineage.Any));
    }

    [Fact]
    public void ChildrenOf_FollowsTheLineage()
    {
        var graph = ParentGraph.Build(
        [
            Set(1, child: 10, KinnectParentageType.Birth, primary: false, (1, ParentRole.Father)),
            Set(2, child: 10, KinnectParentageType.Adopted, primary: true, (3, ParentRole.Father)),
        ]);

        Assert.Empty(graph.ChildrenOf(1));
        Assert.Equal([10], graph.ChildrenOf(1, ParentLineage.BirthOnly));
        Assert.Equal([10], graph.ChildrenOf(3));
        Assert.Empty(graph.ChildrenOf(3, ParentLineage.BirthOnly));
    }

    [Fact]
    public void ParentLabel_SaysHowTheParentIsRecorded()
    {
        var graph = ParentGraph.Build(
        [
            Set(1, child: 10, KinnectParentageType.Adopted, primary: true, (3, ParentRole.Father), (5, ParentRole.Mother)),
            Set(2, child: 10, KinnectParentageType.Foster, primary: false, (4, ParentRole.Mother)),
            Set(3, child: 10, KinnectParentageType.Birth, primary: false, (1, ParentRole.Father)),
        ]);

        Assert.Equal("Adoptive father", graph.ParentLabel(10, 3));
        Assert.Equal("Adoptive mother", graph.ParentLabel(10, 5));
        Assert.Equal("Foster mother", graph.ParentLabel(10, 4, ParentLineage.Any));
        Assert.Equal("Father", graph.ParentLabel(10, 1, ParentLineage.Any));
        Assert.Null(graph.ParentLabel(10, 4));
    }
}

public class ParentSetPrivacyTests
{
    private static readonly TestUserContext Member = new("member");

    private sealed record Family(
        TestDbContextFactory Factory,
        DbContextOptions<ApplicationDbContext> Options,
        Person Jane,
        Person Adam,
        Person Sam,
        Person Gus);

    /// <summary>
    /// Jane has a public birth set (Adam), an administrator-only adopted set (Sam) and a foster set only she can see (Gus).
    /// The adopted set is stored as the primary one.
    /// </summary>
    private static async Task<Family> SeedAsync()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
        Person Make(string name, bool male, string? userId = null) =>
            new() { FamilyName = "Ward", GivenNames = name, IsMale = male, UserId = userId, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var jane = Make("Jane", false, "jane");
        var adam = Make("Adam", true);
        var sam = Make("Sam", true);
        var gus = Make("Gus", true);
        db.People.AddRange(jane, adam, sam, gus);
        await db.SaveChangesAsync();
        await TestParentSets.LinkAsync(db, jane, adam, type: KinnectParentageType.Birth, isPrimary: false);
        await TestParentSets.LinkAsync(db, jane, sam, type: KinnectParentageType.Adopted, isPrimary: true, visibility: LinkVisibility.FamilyAdmins);
        await TestParentSets.LinkAsync(db, jane, gus, type: KinnectParentageType.Foster, isPrimary: false, visibility: LinkVisibility.ChildOnly);
        return new Family(factory, options, jane, adam, sam, gus);
    }

    private static KinnectFamilyGraph GraphFor(TestDbContextFactory factory, IUserContextService user)
    {
        var parentSets = TestParentSets.Create(factory, user);
        var events = new PersonEventService(
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory),
            parentSets);
        var people = new PersonService(
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory),
            new EntityFrameworkRepository<PersonVersion>(factory),
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<PersonLink>(factory),
            new EntityFrameworkRepository<Contact>(factory),
            parentSets);
        return new KinnectFamilyGraph(
            people,
            events,
            parentSets,
            new EntityFrameworkRepository<Person>(factory),
            new EntityFrameworkRepository<PersonEvent>(factory),
            new EntityFrameworkRepository<PersonSpouse>(factory),
            user);
    }

    [Fact]
    public async Task Load_ForAMember_HasNoTraceOfHiddenSets_AndShowsTheVisibleOneAsPrimary()
    {
        var family = await SeedAsync();

        var snapshot = await GraphFor(family.Factory, Member).LoadAsync();

        var jane = snapshot.People.Single(person => person.Id == family.Jane.Id.ToString());
        var set = Assert.Single(jane.ParentSets);
        Assert.Equal(WebParentageType.Birth, set.Type);
        Assert.True(set.IsPrimary);
        Assert.Equal([family.Adam.Id.ToString()], set.ParentIds);
        Assert.Equal([family.Adam.Id.ToString()], jane.Parents);
        Assert.Empty(snapshot.People.Single(person => person.Id == family.Sam.Id.ToString()).Children);
        Assert.Empty(snapshot.People.Single(person => person.Id == family.Gus.Id.ToString()).Children);
    }

    [Fact]
    public async Task Load_ForAnAdministrator_ShowsAdminSetsButNotChildOnlyOnes()
    {
        var family = await SeedAsync();

        var snapshot = await GraphFor(family.Factory, TestUserContext.Admin).LoadAsync();

        var jane = snapshot.People.Single(person => person.Id == family.Jane.Id.ToString());
        Assert.Equal([WebParentageType.Adopted, WebParentageType.Birth], jane.ParentSets.Select(set => set.Type));
        Assert.Equal([family.Sam.Id.ToString()], jane.Parents);
        Assert.Contains(jane.Id, snapshot.People.Single(person => person.Id == family.Sam.Id.ToString()).Children);
    }

    [Fact]
    public async Task Load_ForTheChild_ShowsEverything()
    {
        var family = await SeedAsync();

        var snapshot = await GraphFor(family.Factory, new TestUserContext("jane")).LoadAsync();

        Assert.Equal(2, snapshot.People.Single(person => person.Id == family.Jane.Id.ToString()).ParentSets.Count);
    }

    [Fact]
    public async Task Apply_ParentSetAdded_StoresTheSet_AndANewSetIsNotPrimaryByDefault()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
        var jane = new Person { FamilyName = "Ward", GivenNames = "Jane", IsMale = false, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var adam = new Person { FamilyName = "Ward", GivenNames = "Adam", IsMale = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var sam = new Person { FamilyName = "Hart", GivenNames = "Sam", IsMale = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(jane, adam, sam);
        await db.SaveChangesAsync();
        await TestParentSets.LinkAsync(db, jane, adam);
        var graph = GraphFor(factory, TestUserContext.Admin);
        var before = await graph.LoadAsync();
        var card = before.People.Single(person => person.Id == jane.Id.ToString());
        var edited = card with
        {
            ParentSets =
            [
                .. card.ParentSets,
                new ParentSetData { Id = "new-1", Type = WebParentageType.Adopted, ParentIds = [sam.Id.ToString()], Date = new PartialDate(2001, 6, 14) },
            ],
        };
        var people = before.People.Select(person => person.Id == edited.Id ? edited : person).ToList();

        var result = await graph.ApplyAsync(new FamilyTreeChange(people, null, FamilyTreeChangeKind.ParentSetAdded, PersonId: edited.Id, ParentSetId: "new-1", Type: WebParentageType.Adopted));

        Assert.True(result.IsSuccess);
        await using var check = SqliteDb.CreateContext(options);
        var stored = await check.ParentSets.Include(set => set.Members).Where(set => set.ChildPersonId == jane.Id).OrderBy(set => set.Id).ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.True(stored[0].IsPrimary);
        var adopted = stored[1];
        Assert.False(adopted.IsPrimary);
        Assert.Equal(KinnectParentageType.Adopted, adopted.Type);
        Assert.Equal((short?)2001, adopted.Year);
        Assert.Equal((byte?)14, adopted.Day);
        Assert.Equal(sam.Id, Assert.Single(adopted.Members).ParentPersonId);
    }

    [Fact]
    public async Task Apply_ForAMember_IsForbidden()
    {
        var family = await SeedAsync();
        var graph = GraphFor(family.Factory, Member);

        var result = await graph.ApplyAsync(new FamilyTreeChange([], null, FamilyTreeChangeKind.ParentSetRemoved, PersonId: family.Jane.Id.ToString()));

        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task CloseRelatives_LabelAdoptiveAndFosterParents_AndLeaveOutHiddenOnes()
    {
        var family = await SeedAsync();
        PersonEventService For(IUserContextService user) => new(
            new EntityFrameworkRepository<PersonEvent>(family.Factory),
            new EntityFrameworkRepository<Person>(family.Factory),
            new EntityFrameworkRepository<PersonSpouse>(family.Factory),
            TestParentSets.Create(family.Factory, user));

        var forMember = (await For(Member).GetCloseRelativesAsync(family.Jane.Id)).Value;
        var forAdmin = (await For(TestUserContext.Admin).GetCloseRelativesAsync(family.Jane.Id)).Value;
        var forChild = (await For(new TestUserContext("jane")).GetCloseRelativesAsync(family.Jane.Id)).Value;

        Assert.Equal(["Adam"], forMember.Select(relative => relative.FullName.Split(' ')[0]));
        Assert.Equal("Father", forMember.Single().Relationship);
        Assert.Contains(forAdmin, relative => relative.Relationship == "Adoptive father" && relative.FullName.StartsWith("Sam", StringComparison.Ordinal));
        Assert.DoesNotContain(forAdmin, relative => relative.FullName.StartsWith("Gus", StringComparison.Ordinal));
        Assert.Contains(forChild, relative => relative.Relationship == "Foster father" && relative.FullName.StartsWith("Gus", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CloseRelatives_SiblingsAreNotFoundThroughAHiddenSet()
    {
        var family = await SeedAsync();
        await using var db = SqliteDb.CreateContext(family.Options);
        var half = new Person { FamilyName = "Hart", GivenNames = "Half", IsMale = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(half);
        await db.SaveChangesAsync();
        await TestParentSets.LinkAsync(db, half, family.Sam);
        PersonEventService For(IUserContextService user) => new(
            new EntityFrameworkRepository<PersonEvent>(family.Factory),
            new EntityFrameworkRepository<Person>(family.Factory),
            new EntityFrameworkRepository<PersonSpouse>(family.Factory),
            TestParentSets.Create(family.Factory, user));

        var forMember = (await For(Member).GetCloseRelativesAsync(family.Jane.Id)).Value;
        var forAdmin = (await For(TestUserContext.Admin).GetCloseRelativesAsync(family.Jane.Id)).Value;

        Assert.DoesNotContain(forMember, relative => relative.FullName.StartsWith("Half", StringComparison.Ordinal));
        Assert.Contains(forAdmin, relative => relative.FullName.StartsWith("Half", StringComparison.Ordinal) && relative.Relationship == "Sibling");
    }
}
