using Ironwake.Cli;
using Ironwake.Core;

namespace Ironwake.Client;

/// <summary>
/// One action the camp screen offers as a clickable row (issue 786): the row's text, the
/// <c>campaign</c> command it is, word for word, and what clicking it does on the presenter.
/// </summary>
public sealed record CampAction(string Text, string Command, Func<bool> Run);

/// <summary>
/// The camp actions a mouse reaches without the console (issue 786): every between-map command
/// that changes the record besides buy, bench and march, as rows the renderer draws. A row is
/// offered where its command could be taken (a slot that holds a weapon, a room not built to its
/// limit, a hire on the list, a class the unit meets, a side map open); the record still decides,
/// and a refusal reaches the status line with the console's text. Each row's
/// <see cref="CampAction.Command"/> is what <c>ironwake campaign</c> types for it, so a script of
/// those commands is held to the console by the campaign parity gate.
/// </summary>
public static class CampActions
{
    /// <summary>
    /// The rows for the selected unit <paramref name="unitId"/> (none of the unit's own rows when
    /// null), then the keep's rooms, edits and hires, then the branch's two claimants until one is picked (issue 633), then the side maps, each taking
    /// <paramref name="party"/> as its allies, or the selected unit when the party is empty.
    /// </summary>
    public static IReadOnlyList<CampAction> For(CampaignClient campaign, string? unitId, IReadOnlyList<string> party)
    {
        var actions = new List<CampAction>();
        if (campaign.Over || campaign.NextMap is null)
        {
            return actions;
        }

        var record = campaign.Record;
        var content = campaign.Content;
        if (unitId is not null && record.Find(unitId) is { } unit)
        {
            actions.AddRange(UnitActions(campaign, unit));
        }

        var keep = content.Campaign.Keep;
        foreach (var room in keep.Rooms.Where(r => record.Rooms.Count(id => id == r.Id) < r.Max))
        {
            var id = room.Id;
            actions.Add(new($"build the {room.Name.ToLowerInvariant()} ({room.Price})", $"build {id}", () => campaign.BuildRoom(id)));
        }

        if (record.KeepMenuRefusal(content) is null)
        {
            foreach (var edit in keep.Edits)
            {
                foreach (var at in edit.At.Where(at => !record.Keep.Contains(new KeepWork(edit.Id, at))))
                {
                    var (editId, tile) = (edit.Id, at);
                    actions.Add(new($"build {edit.Name.ToLowerInvariant()} at {at} ({edit.Price})", $"build {editId} {tile}", () => campaign.Build(editId, tile)));
                }
            }
        }

        if (record.Pick is null && record.NextMap(content).Branch is { Count: 2 } branch)
        {
            foreach (var id in branch)
            {
                var claimant = id;
                actions.Add(new($"pick {content.Unit(claimant).Name} for the last seat", $"pick {claimant}", () => campaign.Pick(claimant)));
            }
        }

        foreach (var hire in record.HiresOffered(content))
        {
            var id = hire.Id;
            actions.Add(new($"hire {hire.Name} ({keep.HirePrice})", $"hire {id}", () => campaign.Hire(id)));
        }

        var allies = party.Count > 0 ? party : unitId is null ? Array.Empty<string>() : new[] { unitId };
        if (allies.Count > 0)
        {
            var names = UnitNames.Of(record, content);
            foreach (var quest in record.QuestsOffered(content).Where(q => !record.QuestsTried.Contains(q.Id)))
            {
                var (id, picked) = (quest.Id, allies.ToArray());
                var with = string.Join(", ", picked.Select(a => names[a]));
                actions.Add(new($"fight {names[quest.MemberId]}'s side map {id} with {with}", $"quest {id} {string.Join(" ", picked)}", () => campaign.Quest(id, picked)));
            }
        }

        return actions;
    }

    private static IEnumerable<CampAction> UnitActions(CampaignClient campaign, Unit unit)
    {
        var record = campaign.Record;
        var content = campaign.Content;
        var unitId = unit.Id;
        var forge = record.ForgeBuilt(content);
        for (var i = 0; i < unit.Inventory.Count; i++)
        {
            var (slot, stack) = (i, unit.Inventory.Items[i]);
            var name = content.ItemName(stack.ItemId);
            if (content.Weapons.ContainsKey(stack.ItemId))
            {
                yield return new($"repair slot {slot + 1}: {name}", $"repair {unitId} {slot + 1}", () => campaign.Repair(unitId, slot));
                if (forge)
                {
                    yield return new($"refine slot {slot + 1}: {name}, Mt", $"refine {unitId} {slot + 1} mt", () => campaign.Refine(unitId, slot, "mt"));
                    yield return new($"refine slot {slot + 1}: {name}, hit", $"refine {unitId} {slot + 1} hit", () => campaign.Refine(unitId, slot, "hit"));
                }
            }

            yield return new($"drop slot {slot + 1}: {name}", $"drop {unitId} {slot + 1}", () => campaign.Drop(unitId, slot));
        }

        for (var i = 0; i < record.Wagon.Count; i++)
        {
            var index = i;
            yield return new($"take {content.ItemName(record.Wagon[i])} from the wagon", $"take {unitId} {index + 1}", () => campaign.Take(unitId, index));
        }

        var from = content.Class(unit.ClassId);
        var captain = CampaignRecord.IsCaptain(unit, content);
        foreach (var target in content.Classes.Values.Where(c => c.Id != unit.ClassId && !c.Hidden && (c.Unique is null || c.Unique == unitId)))
        {
            var classId = target.Id;
            if (Certifications.Check(unit, target, from, captain, record.WonQuestIds).Count == 0)
            {
                yield return new($"promote to {target.Name} (the seal)", $"certify {unitId} {classId}", () => campaign.Certify(unitId, classId));
            }

            if (content.Campaign.TrialFor(classId) is not null && record.TrialRefusal(unitId, classId, content) is null)
            {
                yield return new($"play the trial for {target.Name}", $"trial {unitId} {classId}", () => campaign.Trial(unitId, classId));
            }
        }
    }
}
