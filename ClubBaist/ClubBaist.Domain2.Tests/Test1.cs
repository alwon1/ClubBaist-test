namespace ClubBaist.Domain2.Tests;

// ─── Shared helpers ─────────────────────────────────────────────────────────

file static class Builders
{
    public static MembershipLevel Level(int id = 1, string name = "Basic") =>
        new() { Id = id, Name = name };

    public static MemberShipInfo MakeMember(int id, MembershipLevel? level = null) =>
        new() { Id = id, User = null!, MembershipLevel = level ?? Level() };

    public static TeeTimeSlot MakeSlot(DateTime start) => new() { Start = start };

    public static TeeTimeSlot SlotAt(int hour, DayOfWeek? day = null)
    {
        // Default: Saturday 2025-06-14
        var date = day switch
        {
            DayOfWeek.Sunday    => new DateTime(2025, 6, 15),
            DayOfWeek.Monday    => new DateTime(2025, 6, 16),
            _                   => new DateTime(2025, 6, 14) // Saturday
        };
        return new() { Start = date.AddHours(hour) };
    }

    public static TeeTimeBooking MakeBooking(TeeTimeSlot slot, MemberShipInfo member) => new()
    {
        TeeTimeSlot = slot,
        TeeTimeSlotStart = slot.Start,
        BookingMember = member,
        BookingMemberId = member.Id
    };

    public static MembershipLevelTeeTimeAvailability Availability(
        MembershipLevel level, DayOfWeek day, int fromHour, int toHour) =>
        new()
        {
            MembershipLevel = level,
            DayOfWeek = day,
            StartTime = new TimeOnly(fromHour, 0),
            EndTime = new TimeOnly(toHour, 0)
        };

    public static SpecialEvent Event(string name, DateTime start, DateTime end) =>
        new() { Name = name, Start = start, End = end };

    public static Season Season(DateOnly start, DateOnly end) =>
        new() { Name = "Test Season", StartDate = start, EndDate = end };

    public static IQueryable<TeeTimeEvaluation> Seed(
        TeeTimeSlot slot, int spots = int.MaxValue, string? reason = null) =>
        new List<TeeTimeEvaluation> { new(slot, spots, reason) }.AsQueryable();

    public static readonly int Id1 = 1001;
    public static readonly int Id2 = 1002;
    public static readonly int Id3 = 1003;
}

// ─── MaxParticipantsRule ─────────────────────────────────────────────────────
// Note: TeeTimeBooking.ParticipantCount is a DB-computed column (private set),
// so it is always 0 in memory. Tests exercise rule logic; capacity-full path
// can only be verified against a real database.

[TestClass]
public class MaxParticipantsRuleTests
{
    [TestMethod]
    public void EmptySlot_AllowsBooking_WithFullCapacityRemaining()
    {
        var slot    = Builders.SlotAt(8);
        var booking = Builders.MakeBooking(slot, Builders.MakeMember(Builders.Id1));
        var rule    = new MaxParticipantsRule(Enumerable.Empty<TeeTimeBooking>().AsQueryable(), 4);

        var result = rule.Evaluate(Builders.Seed(slot), booking).Single();

        // ParticipantCount is 0 in memory: 4 - 0 existing - 0 incoming = 4
        Assert.AreEqual(4, result.SpotsRemaining);
        Assert.IsNull(result.RejectionReason);
    }

    [TestMethod]
    public void PreservesRejectionReason_FromPriorRule_WhenSlotNotFull()
    {
        var slot    = Builders.SlotAt(8);
        var booking = Builders.MakeBooking(slot, Builders.MakeMember(Builders.Id1));
        var rule    = new MaxParticipantsRule(Enumerable.Empty<TeeTimeBooking>().AsQueryable(), 4);

        // MaxParticipantsRule recalculates SpotsRemaining but preserves RejectionReason
        var result = rule.Evaluate(
            Builders.Seed(slot, -1, "Prior rejection"),
            booking).Single();

        Assert.AreEqual("Prior rejection", result.RejectionReason);
    }

    [TestMethod]
    public void CustomMaxParticipants_IsRespected()
    {
        var slot    = Builders.SlotAt(8);
        var booking = Builders.MakeBooking(slot, Builders.MakeMember(Builders.Id1));
        var rule    = new MaxParticipantsRule(Enumerable.Empty<TeeTimeBooking>().AsQueryable(), 2);

        var result = rule.Evaluate(Builders.Seed(slot), booking).Single();

        Assert.AreEqual(2, result.SpotsRemaining);
    }

    [TestMethod]
    public void ExcludeBookingId_FiltersMatchingBookingFromSum()
    {
        var slot   = Builders.SlotAt(8);
        var member = Builders.MakeMember(Builders.Id1);
        slot.Bookings.Add(Builders.MakeBooking(slot, member)); // Id defaults to 0
        var updated = Builders.MakeBooking(slot, member);
        var rule    = new MaxParticipantsRule(slot.Bookings.AsQueryable(), 4);

        var result = rule.Evaluate(
            Builders.Seed(slot),
            updated, excludeBookingId: 0).Single();

        Assert.AreEqual(4, result.SpotsRemaining);
        Assert.IsNull(result.RejectionReason);
    }
}

// ─── DuplicateBookingRule ────────────────────────────────────────────────────

[TestClass]
public class DuplicateBookingRuleTests
{
    [TestMethod]
    public void NoDuplicates_AllowsBooking()
    {
        var slot = Builders.SlotAt(8);
        slot.Bookings.Add(Builders.MakeBooking(slot, Builders.MakeMember(Builders.Id1)));
        var booking = Builders.MakeBooking(slot, Builders.MakeMember(Builders.Id2));

        var result = new DuplicateBookingRule(slot.Bookings.AsQueryable())
            .Evaluate(Builders.Seed(slot), booking).Single();

        Assert.IsNull(result.RejectionReason);
    }

    [TestMethod]
    public void PrimaryMemberAlreadyBooked_RejectsWithNegativeTwo()
    {
        var slot   = Builders.SlotAt(8);
        var member = Builders.MakeMember(Builders.Id1);
        slot.Bookings.Add(Builders.MakeBooking(slot, member));
        var booking = Builders.MakeBooking(slot, member);

        var result = new DuplicateBookingRule(slot.Bookings.AsQueryable())
            .Evaluate(Builders.Seed(slot), booking).Single();

        Assert.AreEqual(-2, result.SpotsRemaining);
        StringAssert.Contains(result.RejectionReason, "within 2");
    }

    [TestMethod]
    public void IncomingAdditionalParticipant_MatchesPrimaryInExistingBooking_Rejects()
    {
        var slot     = Builders.SlotAt(8);
        var existing = Builders.MakeMember(Builders.Id1);
        slot.Bookings.Add(Builders.MakeBooking(slot, existing));

        var newBooking = Builders.MakeBooking(slot, Builders.MakeMember(Builders.Id2));
        newBooking.AdditionalParticipants.Add(existing); // Id1 is a duplicate

        var result = new DuplicateBookingRule(slot.Bookings.AsQueryable())
            .Evaluate(Builders.Seed(slot), newBooking).Single();

        Assert.AreEqual(-2, result.SpotsRemaining);
    }

    [TestMethod]
    public void IncomingPrimaryMember_MatchesAdditionalInExistingBooking_Rejects()
    {
        var slot      = Builders.SlotAt(8);
        var duplicate = Builders.MakeMember(Builders.Id2);
        var existing  = Builders.MakeBooking(slot, Builders.MakeMember(Builders.Id1));
        existing.AdditionalParticipants.Add(duplicate);
        slot.Bookings.Add(existing);

        var booking = Builders.MakeBooking(slot, duplicate);

        var result = new DuplicateBookingRule(slot.Bookings.AsQueryable())
            .Evaluate(Builders.Seed(slot), booking).Single();

        Assert.AreEqual(-2, result.SpotsRemaining);
    }

    [TestMethod]
    public void IncomingAdditionalParticipant_MatchesAdditionalInExistingBooking_Rejects()
    {
        var slot      = Builders.SlotAt(8);
        var duplicate = Builders.MakeMember(Builders.Id3);
        var existing  = Builders.MakeBooking(slot, Builders.MakeMember(Builders.Id1));
        existing.AdditionalParticipants.Add(duplicate);
        slot.Bookings.Add(existing);

        var newBooking = Builders.MakeBooking(slot, Builders.MakeMember(Builders.Id2));
        newBooking.AdditionalParticipants.Add(duplicate);

        var result = new DuplicateBookingRule(slot.Bookings.AsQueryable())
            .Evaluate(Builders.Seed(slot), newBooking).Single();

        Assert.AreEqual(-2, result.SpotsRemaining);
    }

    [TestMethod]
    public void AlreadyRejected_PassesThrough_WithoutOverwrite()
    {
        var slot   = Builders.SlotAt(8);
        var member = Builders.MakeMember(Builders.Id1);
        slot.Bookings.Add(Builders.MakeBooking(slot, member));
        var booking = Builders.MakeBooking(slot, member); // would be a duplicate

        var result = new DuplicateBookingRule(slot.Bookings.AsQueryable())
            .Evaluate(Builders.Seed(slot, -3, "Event block"), booking).Single();

        Assert.AreEqual(-3, result.SpotsRemaining);
        Assert.AreEqual("Event block", result.RejectionReason);
    }

    [TestMethod]
    public void ExcludeBookingId_AllowsUpdatingOwnBooking()
    {
        var slot   = Builders.SlotAt(8);
        var member = Builders.MakeMember(Builders.Id1);
        slot.Bookings.Add(Builders.MakeBooking(slot, member)); // Id = 0 (default int)
        var updated = Builders.MakeBooking(slot, member);

        var result = new DuplicateBookingRule(slot.Bookings.AsQueryable())
            .Evaluate(Builders.Seed(slot), updated, excludeBookingId: 0).Single();

        Assert.IsNull(result.RejectionReason);
    }

    [TestMethod]
    public void ExistingBookingWithinWindow_Rejects()
    {
        // Member has an existing booking 1 hour before the requested slot — within the 2-hour window.
        var requestedSlot = Builders.SlotAt(8);
        var nearbySlot    = Builders.MakeSlot(requestedSlot.Start.AddHours(-1));
        var member        = Builders.MakeMember(Builders.Id1);
        var nearbyBooking = Builders.MakeBooking(nearbySlot, member);

        var booking = Builders.MakeBooking(requestedSlot, member);

        var result = new DuplicateBookingRule(new[] { nearbyBooking }.AsQueryable())
            .Evaluate(Builders.Seed(requestedSlot), booking).Single();

        Assert.AreEqual(-2, result.SpotsRemaining);
        StringAssert.Contains(result.RejectionReason, "within 2");
    }

    [TestMethod]
    public void ExistingBookingOutsideWindow_Allows()
    {
        // Member has an existing booking 3 hours before the requested slot — outside the 2-hour window.
        var requestedSlot = Builders.SlotAt(8);
        var farSlot       = Builders.MakeSlot(requestedSlot.Start.AddHours(-3));
        var member        = Builders.MakeMember(Builders.Id1);
        var farBooking    = Builders.MakeBooking(farSlot, member);

        var booking = Builders.MakeBooking(requestedSlot, member);

        var result = new DuplicateBookingRule(new[] { farBooking }.AsQueryable())
            .Evaluate(Builders.Seed(requestedSlot), booking).Single();

        Assert.IsNull(result.RejectionReason);
    }

    [TestMethod]
    public void ExistingBookingAtExactWindowBoundary_Allows()
    {
        // Member has an existing booking exactly 2 hours away — boundary is exclusive (<), so allowed.
        var requestedSlot    = Builders.SlotAt(8);
        var boundarySlot     = Builders.MakeSlot(requestedSlot.Start.AddHours(-2));
        var member           = Builders.MakeMember(Builders.Id1);
        var boundaryBooking  = Builders.MakeBooking(boundarySlot, member);

        var booking = Builders.MakeBooking(requestedSlot, member);

        var result = new DuplicateBookingRule(new[] { boundaryBooking }.AsQueryable())
            .Evaluate(Builders.Seed(requestedSlot), booking).Single();

        Assert.IsNull(result.RejectionReason);
    }
}

// ─── MembershipLevelAvailabilityRule ─────────────────────────────────────────

[TestClass]
public class MembershipLevelAvailabilityRuleTests
{
    [TestMethod]
    public void WithinAvailabilityWindow_AllowsBooking()
    {
        var level = Builders.Level();
        var slot  = Builders.SlotAt(9); // Saturday 9am
        var rule  = new MembershipLevelAvailabilityRule(
            new[] { Builders.Availability(level, DayOfWeek.Saturday, 7, 11) }.AsQueryable());

        Assert.IsNull(rule.Evaluate(Builders.Seed(slot), level).Single().RejectionReason);
    }

    [TestMethod]
    public void WrongDayOfWeek_RejectsWithNegativeOne()
    {
        var level = Builders.Level();
        var slot  = Builders.SlotAt(9); // Saturday
        var rule  = new MembershipLevelAvailabilityRule(
            new[] { Builders.Availability(level, DayOfWeek.Sunday, 7, 11) }.AsQueryable());

        var result = rule.Evaluate(Builders.Seed(slot), level).Single();

        Assert.AreEqual(-1, result.SpotsRemaining);
        StringAssert.Contains(result.RejectionReason, level.Name);
    }

    [TestMethod]
    public void BeforeStartTime_RejectsBooking()
    {
        var level = Builders.Level();
        var slot  = Builders.SlotAt(6); // Saturday 6am, starts at 7
        var rule  = new MembershipLevelAvailabilityRule(
            new[] { Builders.Availability(level, DayOfWeek.Saturday, 7, 11) }.AsQueryable());

        Assert.AreEqual(-1, rule.Evaluate(Builders.Seed(slot), level).Single().SpotsRemaining);
    }

    [TestMethod]
    public void AfterEndTime_RejectsBooking()
    {
        var level = Builders.Level();
        var slot  = Builders.SlotAt(13); // Saturday 1pm, ends at noon
        var rule  = new MembershipLevelAvailabilityRule(
            new[] { Builders.Availability(level, DayOfWeek.Saturday, 7, 12) }.AsQueryable());

        Assert.AreEqual(-1, rule.Evaluate(Builders.Seed(slot), level).Single().SpotsRemaining);
    }

    [TestMethod]
    public void AtExactStartTime_AllowsBooking_StartIsInclusive()
    {
        var level = Builders.Level();
        var slot  = Builders.SlotAt(7); // exactly at start
        var rule  = new MembershipLevelAvailabilityRule(
            new[] { Builders.Availability(level, DayOfWeek.Saturday, 7, 11) }.AsQueryable());

        Assert.IsNull(rule.Evaluate(Builders.Seed(slot), level).Single().RejectionReason);
    }

    [TestMethod]
    public void AtExactEndTime_AllowsBooking_EndIsInclusive()
    {
        var level = Builders.Level();
        var slot  = Builders.SlotAt(11); // exactly at end (EndTime >=)
        var rule  = new MembershipLevelAvailabilityRule(
            new[] { Builders.Availability(level, DayOfWeek.Saturday, 7, 11) }.AsQueryable());

        Assert.IsNull(rule.Evaluate(Builders.Seed(slot), level).Single().RejectionReason);
    }

    [TestMethod]
    public void DifferentMembershipLevel_RejectsBooking()
    {
        var basic   = Builders.Level(1, "Basic");
        var premium = Builders.Level(2, "Premium");
        var slot    = Builders.SlotAt(9);
        var rule    = new MembershipLevelAvailabilityRule(
            new[] { Builders.Availability(premium, DayOfWeek.Saturday, 7, 11) }.AsQueryable());

        var result = rule.Evaluate(Builders.Seed(slot), basic).Single();

        Assert.AreEqual(-1, result.SpotsRemaining);
        StringAssert.Contains(result.RejectionReason, basic.Name);
    }

    [TestMethod]
    public void AlreadyRejected_PassesThrough_WithoutOverwrite()
    {
        var level = Builders.Level();
        var slot  = Builders.SlotAt(9);
        var rule  = new MembershipLevelAvailabilityRule(
            new[] { Builders.Availability(level, DayOfWeek.Saturday, 7, 11) }.AsQueryable());

        var result = rule.Evaluate(Builders.Seed(slot, -3, "Event block"), level).Single();

        Assert.AreEqual(-3, result.SpotsRemaining);
        Assert.AreEqual("Event block", result.RejectionReason);
    }

    [TestMethod]
    public void CalledWithBookingContext_DelegatesToMembershipLevelOverload()
    {
        var level   = Builders.Level();
        var member  = Builders.MakeMember(Builders.Id1, level);
        var slot    = Builders.SlotAt(9);
        var booking = Builders.MakeBooking(slot, member);
        var rule    = new MembershipLevelAvailabilityRule(
            new[] { Builders.Availability(level, DayOfWeek.Saturday, 7, 11) }.AsQueryable());

        var fromContext = rule.Evaluate(Builders.Seed(slot), booking).Single();
        var fromLevel   = rule.Evaluate(Builders.Seed(slot), level).Single();

        Assert.AreEqual(fromLevel.SpotsRemaining, fromContext.SpotsRemaining);
        Assert.AreEqual(fromLevel.RejectionReason, fromContext.RejectionReason);
    }
}

// ─── SpecialEventBlockingRule ─────────────────────────────────────────────────

[TestClass]
public class SpecialEventBlockingRuleTests
{
    [TestMethod]
    public void NoEvents_AllowsBooking()
    {
        var slot = Builders.SlotAt(8);
        var rule = new SpecialEventBlockingRule(Enumerable.Empty<SpecialEvent>().AsQueryable());

        Assert.IsNull(rule.Evaluate(Builders.Seed(slot), Builders.Level()).Single().RejectionReason);
    }

    [TestMethod]
    public void SlotWithinEvent_BlocksWithNegativeThreeAndEventName()
    {
        var slot = Builders.SlotAt(9);
        var ev   = Builders.Event("Club Championship", slot.Start.AddHours(-1), slot.Start.AddHours(4));
        var rule = new SpecialEventBlockingRule(new[] { ev }.AsQueryable());

        var result = rule.Evaluate(Builders.Seed(slot), Builders.Level()).Single();

        Assert.AreEqual(-3, result.SpotsRemaining);
        StringAssert.Contains(result.RejectionReason, "Club Championship");
    }

    [TestMethod]
    public void SlotBeforeEvent_AllowsBooking()
    {
        var slot = Builders.SlotAt(7);
        var ev   = Builders.Event("Afternoon Event", slot.Start.AddHours(3), slot.Start.AddHours(6));
        var rule = new SpecialEventBlockingRule(new[] { ev }.AsQueryable());

        Assert.IsNull(rule.Evaluate(Builders.Seed(slot), Builders.Level()).Single().RejectionReason);
    }

    [TestMethod]
    public void SlotAfterEvent_AllowsBooking()
    {
        var slot = Builders.SlotAt(15);
        var ev   = Builders.Event("Morning Event", slot.Start.AddHours(-5), slot.Start.AddHours(-1));
        var rule = new SpecialEventBlockingRule(new[] { ev }.AsQueryable());

        Assert.IsNull(rule.Evaluate(Builders.Seed(slot), Builders.Level()).Single().RejectionReason);
    }

    [TestMethod]
    public void SlotAtExactEventEnd_AllowsBooking_EndIsExclusive()
    {
        // Condition is End > slot.Start, so End == slot.Start does NOT block
        var slot = Builders.SlotAt(12);
        var ev   = Builders.Event("Morning Event", slot.Start.AddHours(-3), slot.Start);
        var rule = new SpecialEventBlockingRule(new[] { ev }.AsQueryable());

        Assert.IsNull(rule.Evaluate(Builders.Seed(slot), Builders.Level()).Single().RejectionReason);
    }

    [TestMethod]
    public void SlotAtExactEventStart_BlocksBooking_StartIsInclusive()
    {
        // Condition is Start <= slot.Start, so Start == slot.Start DOES block
        var slot = Builders.SlotAt(9);
        var ev   = Builders.Event("Tournament", slot.Start, slot.Start.AddHours(4));
        var rule = new SpecialEventBlockingRule(new[] { ev }.AsQueryable());

        Assert.AreEqual(-3, rule.Evaluate(Builders.Seed(slot), Builders.Level()).Single().SpotsRemaining);
    }

    [TestMethod]
    public void AlreadyRejected_PassesThrough_WithoutOverwrite()
    {
        var slot = Builders.SlotAt(9);
        var ev   = Builders.Event("All Day", DateTime.MinValue, DateTime.MaxValue);
        var rule = new SpecialEventBlockingRule(new[] { ev }.AsQueryable());

        var result = rule.Evaluate(Builders.Seed(slot, -2, "Duplicate"), Builders.Level()).Single();

        Assert.AreEqual(-2, result.SpotsRemaining);
        Assert.AreEqual("Duplicate", result.RejectionReason);
    }

    [TestMethod]
    public void BothOverloads_ProduceSameResult()
    {
        var slot    = Builders.SlotAt(9);
        var ev      = Builders.Event("Tournament", slot.Start.AddHours(-1), slot.Start.AddHours(3));
        var rule    = new SpecialEventBlockingRule(new[] { ev }.AsQueryable());
        var level   = Builders.Level();
        var member  = Builders.MakeMember(Builders.Id1, level);
        var booking = Builders.MakeBooking(slot, member);

        var fromContext = rule.Evaluate(Builders.Seed(slot), booking).Single();
        var fromLevel   = rule.Evaluate(Builders.Seed(slot), level).Single();

        Assert.AreEqual(fromLevel.SpotsRemaining, fromContext.SpotsRemaining);
        Assert.AreEqual(fromLevel.RejectionReason, fromContext.RejectionReason);
    }
}

// ─── BookingRuleExtensions ────────────────────────────────────────────────────

[TestClass]
public class BookingRuleExtensionsTests
{
    [TestMethod]
    public void EvaluateWithContext_FiltersToBookingSlotOnly()
    {
        var level   = Builders.Level();
        var member  = Builders.MakeMember(Builders.Id1, level);
        var slot1   = Builders.SlotAt(8);
        var slot2   = Builders.SlotAt(9);
        var booking = Builders.MakeBooking(slot1, member);

        var results = new List<TeeTimeSlot> { slot1, slot2 }.AsQueryable()
            .Evaluate(Enumerable.Empty<IBookingRule>(), booking)
            .ToList();

        Assert.HasCount(1, results);
        Assert.AreEqual(slot1.Start, results[0].Slot.Start);
    }

    [TestMethod]
    public void EvaluateWithMembershipLevel_IncludesAllSlots()
    {
        var level = Builders.Level();
        var slot1 = Builders.SlotAt(8);
        var slot2 = Builders.SlotAt(9);

        var results = new List<TeeTimeSlot> { slot1, slot2 }.AsQueryable()
            .Evaluate(Enumerable.Empty<IBookingRule>(), level)
            .ToList();

        Assert.HasCount(2, results);
    }

    [TestMethod]
    public void EvaluateWithNoRules_SetsInitialSpotsToMaxValue()
    {
        var slot    = Builders.SlotAt(8);
        var results = new List<TeeTimeSlot> { slot }.AsQueryable()
            .Evaluate(Enumerable.Empty<IBookingRule>(), Builders.Level())
            .ToList();

        Assert.AreEqual(int.MaxValue, results[0].SpotsRemaining);
        Assert.IsNull(results[0].RejectionReason);
    }

    [TestMethod]
    public void EvaluateWithChainedRules_LaterRuleSeesEarlierResult()
    {
        // MembershipLevelAvailabilityRule allows, SpecialEventBlockingRule then rejects
        var level = Builders.Level();
        var slot  = Builders.SlotAt(9); // Saturday — availability matches, event covers whole day
        var ev    = Builders.Event("Closed", slot.Start.AddHours(-1), slot.Start.AddHours(4));
        var rules = new List<IBookingRule>
        {
            new MembershipLevelAvailabilityRule(
                new[] { Builders.Availability(level, DayOfWeek.Saturday, 7, 11) }.AsQueryable()),
            new SpecialEventBlockingRule(new[] { ev }.AsQueryable())
        };

        var result = new List<TeeTimeSlot> { slot }.AsQueryable()
            .Evaluate(rules, level).Single();

        Assert.AreEqual(-3, result.SpotsRemaining);
        StringAssert.Contains(result.RejectionReason, "Closed");
    }

    [TestMethod]
    public void EvaluateWithChainedRules_EarlyRejectionPassesThroughLaterRules()
    {
        // MembershipLevelAvailabilityRule rejects (Sunday, only Saturday allowed)
        // SpecialEventBlockingRule allows (no events), but must pass through the -1 unchanged
        var level  = Builders.Level();
        var slot   = Builders.SlotAt(9, DayOfWeek.Sunday);
        var rules  = new List<IBookingRule>
        {
            new MembershipLevelAvailabilityRule(
                new[] { Builders.Availability(level, DayOfWeek.Saturday, 7, 11) }.AsQueryable()),
            new SpecialEventBlockingRule(Enumerable.Empty<SpecialEvent>().AsQueryable())
        };

        var result = new List<TeeTimeSlot> { slot }.AsQueryable()
            .Evaluate(rules, level).Single();

        Assert.AreEqual(-1, result.SpotsRemaining);
    }
}

// ─── PastSlotRule ────────────────────────────────────────────────────────────

[TestClass]
public class PastSlotRuleTests
{
    [TestMethod]
    public void FutureSlot_AllowsBooking()
    {
        var slot    = new TeeTimeSlot { Start = DateTime.UtcNow.AddHours(1) };
        var booking = Builders.MakeBooking(slot, Builders.MakeMember(Builders.Id1));

        var result = new PastSlotRule()
            .Evaluate(Builders.Seed(slot), booking).Single();

        Assert.IsNull(result.RejectionReason);
    }

    [TestMethod]
    public void PastSlot_RejectsWithNegativeFive()
    {
        var slot    = new TeeTimeSlot { Start = DateTime.UtcNow.AddHours(-1) };
        var booking = Builders.MakeBooking(slot, Builders.MakeMember(Builders.Id1));

        var result = new PastSlotRule()
            .Evaluate(Builders.Seed(slot), booking).Single();

        Assert.AreEqual(-5, result.SpotsRemaining);
        StringAssert.Contains(result.RejectionReason, "past");
    }

    [TestMethod]
    public void AlreadyRejected_PassesThrough_WithoutOverwrite()
    {
        var slot    = new TeeTimeSlot { Start = DateTime.UtcNow.AddHours(-1) };
        var booking = Builders.MakeBooking(slot, Builders.MakeMember(Builders.Id1));

        var result = new PastSlotRule()
            .Evaluate(Builders.Seed(slot, -3, "Event block"), booking).Single();

        Assert.AreEqual(-3, result.SpotsRemaining);
        Assert.AreEqual("Event block", result.RejectionReason);
    }

    [TestMethod]
    public void AvailabilityQuery_ByMembershipLevel_NotFiltered()
    {
        var slot  = new TeeTimeSlot { Start = DateTime.UtcNow.AddHours(-1) };
        var level = Builders.Level();

        // The MembershipLevel overload has the default no-op — past slots still show for availability queries
        IBookingRule rule = new PastSlotRule();
        var result = rule.Evaluate(Builders.Seed(slot), level).Single();

        Assert.IsNull(result.RejectionReason);
    }
}
