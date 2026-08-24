using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Ledger;

namespace LoyaltyLab.Domain.Tests.Ledger;

/// <summary>
/// Property-based coverage of the five ledger invariants in docs/02 §3.2 (G4, G6).
/// Each seed is a full randomized sequence; failures name the seed so they reproduce.
/// </summary>
public sealed class LedgerInvariantTests
{
    private const int CaseCount = 1_000;

    [Fact]
    public void One_thousand_random_sequences_preserve_the_five_invariants()
    {
        for (var seed = 0; seed < CaseCount; seed++)
        {
            var captured = seed;
            Action act = () => Sequence.Run(new Random(captured));
            act.Should().NotThrow($"seed {captured}");
        }
    }

    private sealed class Sequence
    {
        private readonly Random _rng;
        private readonly MutableClock _clock = new(new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero));
        private readonly List<LedgerTransaction> _history = [];
        private readonly Dictionary<string, LedgerTransaction> _byKey = new(StringComparer.Ordinal);
        private int _keys;

        private Sequence(Random rng)
        {
            _rng = rng;
            var partner = PartnerId.New();
            Members =
            [
                LedgerAccount.MemberCredits(partner, MemberId.New()),
                LedgerAccount.MemberCredits(partner, MemberId.New()),
            ];
            Issuance = LedgerAccount.Issuance(partner);
            Redemption = LedgerAccount.Redemption(partner);
            Breakage = LedgerAccount.Breakage(partner);
        }

        private IReadOnlyList<LedgerAccount> Members { get; }

        private LedgerAccount Issuance { get; }

        private LedgerAccount Redemption { get; }

        private LedgerAccount Breakage { get; }

        public static void Run(Random rng)
        {
            var sequence = new Sequence(rng);
            var steps = rng.Next(1, 41);
            for (var i = 0; i < steps; i++)
            {
                sequence.Step();
            }

            if (sequence._byKey.Count > 0)
            {
                sequence.Replay();
            }

            sequence.AssertInvariants();
        }

        private void Step()
        {
            if (_byKey.Count > 0 && _rng.Next(6) == 0)
            {
                Replay();
                return;
            }

            switch (_rng.Next(5))
            {
                case 0:
                    Earn();
                    break;
                case 1:
                    Spend(burn: true);
                    break;
                case 2:
                    Spend(burn: false);
                    break;
                case 3:
                    Adjust();
                    break;
                default:
                    Reverse();
                    break;
            }
        }

        private void Earn()
        {
            var member = PickMember();
            Accept(LedgerTransaction.Earn(member, Issuance, Credits(), NextKey(), "Grant", _clock));
        }

        private void Spend(bool burn)
        {
            var member = PickMember();
            var balance = Balance(member);
            if (balance <= 0)
            {
                return;
            }

            var credits = _rng.Next(1, balance + 1);
            if (burn)
            {
                Accept(LedgerTransaction.Burn(member, Redemption, credits, NextKey(), "Tender", _clock));
            }
            else
            {
                Accept(LedgerTransaction.Expire(member, Breakage, credits, NextKey(), "Lapse", _clock));
            }
        }

        private void Adjust()
        {
            var member = PickMember();
            var delta = _rng.Next(-200, 201);
            if (delta == 0 || (delta < 0 && -delta > Balance(member)))
            {
                return;
            }

            Accept(LedgerTransaction.Adjust(member, Issuance, delta, NextKey(), "Correction", _clock));
        }

        private void Reverse()
        {
            var candidates = _history
                .Where(transaction =>
                    transaction.Type != LedgerTransactionType.Reversal
                    && !_history.Any(other =>
                        other.Type == LedgerTransactionType.Reversal
                        && other.ReversesTransactionId == transaction.Id)
                    && !WouldMakeMemberNegative(transaction))
                .ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            Accept(LedgerTransaction.Reverse(candidates[_rng.Next(candidates.Count)], NextKey(), "Undo", _clock));
        }

        private void Replay()
        {
            var original = _byKey.Values.ElementAt(_rng.Next(_byKey.Count));
            var before = Capture();

            _byKey.TryGetValue(original.IdempotencyKey, out var replayed).Should().BeTrue();
            replayed.Should().BeSameAs(original);
            _history.Should().HaveCount(before.Count);
            _history.Select(transaction => transaction.Id.Value).Should().Equal(before.Ids);
            Members.Select(Balance).Should().Equal(before.MemberBalances);
            AssertInvariants();
        }

        private void Accept(LedgerTransaction posted)
        {
            _history.Add(posted);
            _byKey.Add(posted.IdempotencyKey, posted);
            _clock.UtcNow = _clock.UtcNow.AddSeconds(1);
            AssertInvariants();
        }

        private void AssertInvariants()
        {
            foreach (var transaction in _history)
            {
                transaction.Entries.Sum(entry => entry.Amount).Should().Be(
                    0,
                    "invariant 1: every transaction is balanced.");
            }

            foreach (var member in Members)
            {
                Balance(member).Should().BeGreaterThanOrEqualTo(
                    0,
                    "invariant 2: a member's derived balance is never negative.");
            }

            var reversals = _history.Where(transaction => transaction.Type == LedgerTransactionType.Reversal).ToList();
            var reversedIds = new HashSet<LedgerTransactionId>();
            foreach (var reversal in reversals)
            {
                reversal.ReversesTransactionId.Should().NotBeNull(
                    "invariant 3: every reversal names exactly one prior transaction.");
                var original = _history.SingleOrDefault(item => item.Id == reversal.ReversesTransactionId);
                original.Should().NotBeNull("invariant 3: every reversal names exactly one prior transaction.");
                original!.Type.Should().NotBe(
                    LedgerTransactionType.Reversal,
                    "invariant 3: a reversal is never itself reversed.");
                reversedIds.Add(reversal.ReversesTransactionId!.Value).Should().BeTrue(
                    "invariant 3: no transaction is reversed twice.");
            }

            var issued = -Balance(Issuance);
            var burned = Balance(Redemption);
            var expired = Balance(Breakage);
            var outstanding = Members.Sum(Balance);
            (issued - burned - expired).Should().Be(
                outstanding,
                "invariant 4: issued − burned − expired equals outstanding liability.");
        }

        private bool WouldMakeMemberNegative(LedgerTransaction original) =>
            original.Entries.Any(entry =>
            {
                var account = Account(entry.AccountId);
                return account.Type == LedgerAccountType.MemberCredits
                    && Balance(account) - entry.Amount < 0;
            });

        private LedgerSnapshot Capture() =>
            new(
                _history.Count,
                _history.Select(transaction => transaction.Id.Value).ToArray(),
                Members.Select(Balance).ToArray());

        private LedgerAccount PickMember() => Members[_rng.Next(Members.Count)];

        private int Credits() => _rng.Next(1, 401);

        private string NextKey() => $"op-{_keys++}";

        private int Balance(LedgerAccount account) => LedgerBalances.For(account.Id, _history);

        private LedgerAccount Account(LedgerAccountId id) =>
            Members.Concat([Issuance, Redemption, Breakage]).Single(account => account.Id == id);
    }

    private sealed record LedgerSnapshot(int Count, Guid[] Ids, int[] MemberBalances);
}
