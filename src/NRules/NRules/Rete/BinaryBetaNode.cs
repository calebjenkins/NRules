using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NRules.Rete
{
    internal abstract class BinaryBetaNode : BetaNode, IObjectSink
    {
        private static readonly TupleFactSet[] EmptySetList = new TupleFactSet[0];
        private static readonly Dictionary<long, List<Fact>> EmptyGroups = new Dictionary<long, List<Fact>>();

        public ITupleSource LeftSource { get; }
        public IObjectSource RightSource { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public IList<IBetaCondition> Conditions { get; }

        protected BinaryBetaNode(ITupleSource leftSource, IObjectSource rightSource)
        {
            LeftSource = leftSource;
            RightSource = rightSource;

            LeftSource.Attach(this);
            RightSource.Attach(this);

            Conditions = new List<IBetaCondition>();
        }

        public abstract void PropagateAssert(IExecutionContext context, IList<Fact> facts);
        public abstract void PropagateUpdate(IExecutionContext context, IList<Fact> facts);
        public abstract void PropagateRetract(IExecutionContext context, IList<Fact> facts);

        protected bool MatchesConditions(IExecutionContext context, Tuple left, Fact right)
        {
            foreach (var condition in Conditions)
            {
                if (!condition.IsSatisfiedBy(context, left, right)) return false;
            }
            return true;
        }

        protected IEnumerable<TupleFactSet> JoinedSets(IExecutionContext context, IList<Tuple> tuples)
        {
            if (tuples.Count == 0) return EmptySetList;
            int level = tuples[0].Level;

            var facts = Enumerable.ToList<Fact>(RightSource.GetFacts(context));
            if (facts.Count > 0)
            {
                IDictionary<long, List<Fact>> factGroups = GroupFacts(facts, level);
                if (factGroups.Count > 0)
                    return JoinByGroupId(tuples, factGroups);
            }

            return CrossJoin(tuples, facts);
        }

        protected IEnumerable<TupleFactSet> JoinedSets(IExecutionContext context, IList<Fact> facts)
        {
            var tuples = Enumerable.ToList<Tuple>(LeftSource.GetTuples(context));
            if (tuples.Count == 0) return EmptySetList;
            int level = tuples[0].Level;

            IDictionary<long, List<Fact>> factGroups = GroupFacts(facts, level);
            if (factGroups.Count > 0)
                return JoinByGroupId(tuples, factGroups);
            
            return CrossJoin(tuples, facts);
        }

        private IEnumerable<TupleFactSet> JoinByGroupId(IEnumerable<Tuple> tuples, IDictionary<long, List<Fact>> factGroups)
        {
            var sets = new List<TupleFactSet>();
            foreach (var tuple in tuples)
            {
                List<Fact> tupleFacts;
                var tupleFactSet = factGroups.TryGetValue(tuple.Id, out tupleFacts)
                    ? new TupleFactSet(tuple, tupleFacts)
                    : new TupleFactSet(tuple, new List<Fact>());
                sets.Add(tupleFactSet);
            }
            return sets;
        }

        private IEnumerable<TupleFactSet> CrossJoin(IList<Tuple> tuples, IList<Fact> facts)
        {
            var sets = new List<TupleFactSet>();
            foreach (var tuple in tuples)
            {
                sets.Add(new TupleFactSet(tuple, facts));
            }
            return sets;
        }

        private IDictionary<long, List<Fact>> GroupFacts(IList<Fact> facts, int level)
        {
            if (facts.Count == 0 || !facts[0].IsWrapperFact) return EmptyGroups;

            //This can be further optimized by grouping tuples by GroupId
            //and only descending to parent tuples once per group
            var factGroups = new Dictionary<long, List<Fact>>();
            foreach (var fact in facts)
            {
                var wrapperfact = (WrapperFact) fact;
                long groupId = wrapperfact.WrappedTuple.GetGroupId(level);
                List<Fact> factGroup;
                if (!factGroups.TryGetValue(groupId, out factGroup))
                {
                    factGroup = new List<Fact>();
                    factGroups[groupId] = factGroup;
                }
                factGroup.Add(wrapperfact);
            }
            return factGroups;
        }
    }
}