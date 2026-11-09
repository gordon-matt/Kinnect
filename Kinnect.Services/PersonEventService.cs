namespace Kinnect.Services;

public class PersonEventService(
    IRepository<PersonEvent> eventRepository,
    IRepository<Person> personRepository,
    IRepository<PersonSpouse> spouseRepository,
    IParentSetService parentSets) : IPersonEventService
{
    public async Task<Result<PersonEventDto>> CopyToPersonAsync(int sourceEventId, int targetPersonId)
    {
        var source = await eventRepository.FindOneAsync(sourceEventId);
        if (source is null)
        {
            return Result.NotFound("Event not found.");
        }

        if (await HasSingleInstanceConflictAsync(targetPersonId, source.EventType))
        {
            return Result.Invalid(new ValidationError(
                $"{PersonEventType.GetLabel(source.EventType)} can only be added once."));
        }

        var copy = await eventRepository.InsertAsync(new PersonEvent
        {
            PersonId = targetPersonId,
            EventType = source.EventType,
            Year = source.Year,
            Month = source.Month,
            Day = source.Day,
            Place = source.Place,
            Latitude = source.Latitude,
            Longitude = source.Longitude,
            Description = source.Description,
            Note = source.Note,
            CreatedAtUtc = DateTime.UtcNow
        });

        return Result.Success(copy.ToDto());
    }

    public async Task<Result<int>> CopyToCloseRelativesAsync(
        int sourceEventId,
        IReadOnlyCollection<int> targetPersonIds,
        string currentUserId,
        bool isAdmin = false,
        bool isEditorOrAbove = false)
    {
        if (targetPersonIds.Count == 0)
        {
            return Result.Invalid(new ValidationError("Choose at least one relative."));
        }

        var source = await eventRepository.FindOneAsync(sourceEventId);
        if (source is null)
        {
            return Result.NotFound("Event not found.");
        }

        if (!PersonEventType.CanCopyToRelatives(source.EventType))
        {
            return Result.Invalid(new ValidationError(
                $"{PersonEventType.GetLabel(source.EventType)} belongs to one person and cannot be copied."));
        }

        var relatives = await GetCloseRelativesAsync(source.PersonId);
        if (!relatives.IsSuccess)
        {
            return Result<int>.NotFound(relatives.Errors.FirstOrDefault() ?? "Person not found.");
        }

        var allowed = relatives.Value.Select(relative => relative.PersonId).ToHashSet();
        int copied = 0;
        int denied = 0;
        int duplicates = 0;

        foreach (int targetId in targetPersonIds.Distinct())
        {
            if (!allowed.Contains(targetId))
            {
                continue;
            }

            var target = await personRepository.FindOneAsync(targetId);
            if (target is null)
            {
                continue;
            }

            bool canEdit = target.UserId == currentUserId
                || isAdmin
                || (isEditorOrAbove && target.UserId is null);
            if (!canEdit)
            {
                denied++;
                continue;
            }

            if (await HasSameEventAsync(targetId, source))
            {
                duplicates++;
                continue;
            }

            await eventRepository.InsertAsync(new PersonEvent
            {
                PersonId = targetId,
                EventType = source.EventType,
                Year = source.Year,
                Month = source.Month,
                Day = source.Day,
                Place = source.Place,
                Latitude = source.Latitude,
                Longitude = source.Longitude,
                Description = source.Description,
                Note = source.Note,
                CreatedAtUtc = DateTime.UtcNow
            });
            copied++;
        }

        return copied > 0
            ? Result.Success(copied)
            : denied > 0
            ? (Result<int>)Result.Forbidden()
            : duplicates > 0 ? Result.Success(0) : (Result<int>)Result.Invalid(new ValidationError("Choose a spouse, parent, sibling, or child."));
    }

    /// <summary>
    /// Spouses, parents, children and siblings the signed-in viewer can see. Parents and children are listed through
    /// every set of parents by default, each labelled with how the link is recorded (Father, Adoptive mother, Foster
    /// child), because a profile should show who the person's family is. Pass <see cref="ParentLineage.BirthOnly"/> for
    /// blood relatives only, or <see cref="ParentLineage.Primary"/> to follow just the family the tree draws.
    /// Hidden sets are not in the graph, so a hidden link never adds anybody here.
    /// </summary>
    public async Task<Result<IReadOnlyList<CloseRelativeDto>>> GetCloseRelativesAsync(int personId, ParentLineage lineage = ParentLineage.Any)
    {
        var person = await personRepository.FindOneAsync(personId);
        if (person is null)
        {
            return Result.NotFound("Person not found.");
        }

        var spouseLinks = await spouseRepository.FindAsync(new SearchOptions<PersonSpouse>
        {
            Query = x => x.PersonId == personId || x.SpouseId == personId
        });

        var spouseIds = spouseLinks
            .Select(link => link.PersonId == personId ? link.SpouseId : link.PersonId)
            .ToHashSet();

        var graph = await parentSets.LoadGraphAsync();
        var labels = new Dictionary<int, string>();
        foreach (int spouseId in spouseIds)
        {
            labels[spouseId] = "Spouse";
        }

        foreach (int parentId in graph.ParentsOf(personId, lineage))
        {
            labels.TryAdd(parentId, graph.ParentLabel(personId, parentId, lineage) ?? "Parent");
        }

        foreach (int childId in graph.ChildrenOf(personId, lineage))
        {
            var type = graph.Followed(childId, lineage).First(set => set.Members.Any(member => member.PersonId == personId)).Type;
            labels.TryAdd(childId, type switch
            {
                ParentageType.Adopted => "Adopted child",
                ParentageType.Foster => "Foster child",
                _ => "Child",
            });
        }

        foreach (int siblingId in graph.SiblingsOf(personId, lineage))
        {
            labels.TryAdd(siblingId, "Sibling");
        }

        labels.Remove(personId);
        var ids = labels.Keys.ToList();
        var found = ids.Count == 0
            ? []
            : (await personRepository.FindAsync(new SearchOptions<Person> { Query = other => ids.Contains(other.Id) })).ToList();

        string[] order = ["Spouse", "Father", "Mother", "Adoptive father", "Adoptive mother", "Foster father", "Foster mother", "Parent", "Child", "Adopted child", "Foster child", "Sibling"];
        IReadOnlyList<CloseRelativeDto> relatives = found
            .OrderBy(other => Array.IndexOf(order, labels[other.Id]))
            .ThenBy(other => other.GivenNames, StringComparer.OrdinalIgnoreCase)
            .ThenBy(other => other.FamilyName, StringComparer.OrdinalIgnoreCase)
            .Select(other => new CloseRelativeDto
            {
                PersonId = other.Id,
                FullName = PersonDisplay.Name(other),
                Relationship = labels[other.Id],
            })
            .ToList();

        return Result.Success(relatives);
    }
    public async Task<Result<PersonEventDto>> CreateAsync(int personId, PersonEventRequest request)
    {
        string eventType = request.EventType.ToUpperInvariant();
        if (PersonEventType.IsNonTimelineEventType(eventType))
        {
            return Result.Invalid(new ValidationError("This event type is not stored on the timeline."));
        }

        if (await HasSingleInstanceConflictAsync(personId, eventType))
        {
            return Result.Invalid(new ValidationError(
                $"{PersonEventType.GetLabel(eventType)} can only be added once."));
        }

        var evt = await eventRepository.InsertAsync(new PersonEvent
        {
            PersonId = personId,
            EventType = eventType,
            Year = request.Year,
            Month = request.Month,
            Day = request.Day,
            Place = request.Place,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Description = request.Description,
            Note = request.Note,
            CreatedAtUtc = DateTime.UtcNow
        });

        return Result.Success(evt.ToDto());
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var evt = await eventRepository.FindOneAsync(id);
        if (evt is null)
        {
            return Result.NotFound("Event not found.");
        }

        await eventRepository.DeleteAsync(evt);
        return Result.Success();
    }

    public async Task<Result<PersonEventDto>> GetByIdAsync(int id)
    {
        var evt = await eventRepository.FindOneAsync(id);
        return evt is null
            ? Result.NotFound("Event not found.")
            : Result.Success(evt.ToDto());
    }

    public async Task<Result<IEnumerable<PersonEventDto>>> GetByPersonAsync(int personId)
    {
        var events = await eventRepository.FindAsync(new SearchOptions<PersonEvent>
        {
            Query = x => x.PersonId == personId
        });

        return Result.Success(events
            .Where(e => !PersonEventType.IsNonTimelineEventType(e.EventType))
            .OrderBy(e => e.Year ?? 9999)
            .ThenBy(e => e.Month ?? 0)
            .ThenBy(e => e.Day ?? 0)
            .Select(e => e.ToDto()));
    }

    public async Task<Result<PersonEventDto>> UpdateAsync(int id, PersonEventRequest request)
    {
        var evt = await eventRepository.FindOneAsync(id);
        if (evt is null)
        {
            return Result.NotFound("Event not found.");
        }

        string eventType = request.EventType.ToUpperInvariant();

        if (PersonEventType.IsNonTimelineEventType(eventType))
        {
            return Result.Invalid(new ValidationError("This event type is not stored on the timeline."));
        }

        if (await HasSingleInstanceConflictAsync(evt.PersonId, eventType, evt.Id))
        {
            return Result.Invalid(new ValidationError(
                $"{PersonEventType.GetLabel(eventType)} can only be added once."));
        }

        evt.EventType = eventType;
        evt.Year = request.Year;
        evt.Month = request.Month;
        evt.Day = request.Day;
        evt.Place = request.Place;
        evt.Latitude = request.Latitude;
        evt.Longitude = request.Longitude;
        evt.Description = request.Description;
        evt.Note = request.Note;

        await eventRepository.UpdateAsync(evt);
        return Result.Success(evt.ToDto());
    }

    private async Task<bool> HasSingleInstanceConflictAsync(int personId, string eventType, int? excludingEventId = null) => PersonEventType.IsSingleInstanceTimelineEventType(eventType) && await eventRepository.ExistsAsync(e =>
                                                                                                                                      e.PersonId == personId &&
                                                                                                                                      e.EventType == eventType &&
                                                                                                                                      (
                                                                                                                                          !excludingEventId.HasValue ||
                                                                                                                                          e.Id != excludingEventId.Value
                                                                                                                                      ));

    private async Task<bool> HasSameEventAsync(int personId, PersonEvent source)
    {
        var existing = await eventRepository.FindAsync(new SearchOptions<PersonEvent>
        {
            Query = evt => evt.PersonId == personId && evt.EventType == source.EventType
        });

        return existing.Any(evt =>
            evt.Year == source.Year
            && evt.Month == source.Month
            && evt.Day == source.Day
            && SameText(evt.Place, source.Place)
            && SameText(evt.Description, source.Description));
    }

    private static bool SameText(string? left, string? right) =>
        string.Equals((left ?? string.Empty).Trim(), (right ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
}