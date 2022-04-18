﻿using Core.Goals;
using System.Collections.Generic;
using System.Linq;

namespace Core.GOAP
{
    /**
	 * Plans what actions can be completed in order to fulfill a goal state.
	 */

    public class GoapPlanner
    {
        public static void RefreshState(IEnumerable<GoapGoal> availableActions)
        {
            foreach (GoapGoal a in availableActions)
            {
                a.SetState(InState(a.Preconditions, new()));
            }
        }

        /**
		 * Plan what sequence of actions can fulfill the goal.
		 * Returns null if a plan could not be found, or a list of the actions
		 * that must be performed, in order, to fulfill the goal.
		 */

        public Stack<GoapGoal> Plan(IEnumerable<GoapGoal> availableActions,
            HashSet<KeyValuePair<GoapKey, bool>> worldState,
            HashSet<KeyValuePair<GoapKey, GoapPreCondition>> goal)
        {
            Node root = new(null, 0, worldState, null);

            // check what actions can run using their checkProceduralPrecondition
            HashSet<GoapGoal> usableActions = new();
            foreach (GoapGoal a in availableActions)
            {
                if (a.CheckIfActionCanRun())
                {
                    usableActions.Add(a);
                }
                else
                {
                    a.SetState(InState(a.Preconditions, root.state));
                }
            }

            // build up the tree and record the leaf nodes that provide a solution to the goal.
            List<Node> leaves = new();
            if (!BuildGraph(root, leaves, usableActions, goal))
            {
                return new();
            }

            // get the cheapest leaf
            Stack<GoapGoal> result = new();
            Node? node = leaves.MinBy(a => a.runningCost);
            while (node != null)
            {
                if (node.action != null)
                {
                    result.Push(node.action);
                }
                node = node.parent;
            }
            return result;
        }

        /**
		 * Returns true if at least one solution was found.
		 * The possible paths are stored in the leaves list. Each leaf has a
		 * 'runningCost' value where the lowest cost will be the best action
		 * sequence.
		 */

        private bool BuildGraph(Node parent, List<Node> leaves, HashSet<GoapGoal> usableActions, HashSet<KeyValuePair<GoapKey, GoapPreCondition>> goal)
        {
            bool foundOne = false;

            // go through each action available at this node and see if we can use it here
            foreach (GoapGoal action in usableActions)
            {
                // if the parent state has the conditions for this action's preconditions, we can use it here
                var result = InState(action.Preconditions, parent.state);
                action.SetState(result);

                if (!result.ContainsValue(false))
                {
                    // apply the action's effects to the parent state
                    var currentState = PopulateState(parent.state, action.Effects);
                    //Debug.Log(GoapAgent.prettyPrint(currentState));
                    Node node = new(parent, parent.runningCost + action.CostOfPerformingAction, currentState, action);

                    result = InState(goal, currentState);
                    if (!result.ContainsValue(false))
                    {
                        // we found a solution!
                        leaves.Add(node);
                        foundOne = true;
                    }
                    else
                    {
                        // not at a solution yet, so test all the remaining actions and branch out the tree
                        HashSet<GoapGoal> subset = ActionSubset(usableActions, action);
                        bool found = BuildGraph(node, leaves, subset, goal);
                        if (found)
                        {
                            foundOne = true;
                        }
                    }
                }
            }

            return foundOne;
        }

        /**
		 * Create a subset of the actions excluding the removeMe one. Creates a new set.
		 */

        private static HashSet<GoapGoal> ActionSubset(HashSet<GoapGoal> actions, GoapGoal removeMe)
        {
            HashSet<GoapGoal> subset = new();
            foreach (GoapGoal a in actions)
            {
                if (!a.Equals(removeMe))
                    subset.Add(a);
            }
            return subset;
        }

        /**
		 * Check that all items in 'test' are in 'state'. If just one does not match or is not there
		 * then this returns false.
		 */

        private static Dictionary<string, bool> InState(HashSet<KeyValuePair<GoapKey, GoapPreCondition>> test, HashSet<KeyValuePair<GoapKey, bool>> state)
        {
            Dictionary<string, bool> resultState = new();
            foreach (var t in test)
            {
                bool found = false;
                foreach (var s in state)
                {
                    found = s.Key == t.Key;
                    if (found)
                    {
                        resultState.Add(t.Value.Description, s.Value.Equals(t.Value.State));
                        break;
                    }
                }

                if (!found)
                {
                    resultState.Add(t.Value.Description, false);
                }
            }
            return resultState;
        }

        /**
		 * Apply the stateChange to the currentState
		 */

        private static HashSet<KeyValuePair<GoapKey, bool>> PopulateState(HashSet<KeyValuePair<GoapKey, bool>> currentState, HashSet<KeyValuePair<GoapKey, bool>> stateChange)
        {
            HashSet<KeyValuePair<GoapKey, bool>> state = new();
            // copy the KVPs over as new objects
            foreach (var s in currentState)
            {
                state.Add(new(s.Key, s.Value));
            }

            foreach (var change in stateChange)
            {
                // if the key exists in the current state, update the Value
                bool exists = false;

                foreach (var s in state)
                {
                    if (s.Equals(change))
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                {
                    state.RemoveWhere((KeyValuePair<GoapKey, bool> kvp) => { return kvp.Key.Equals(change.Key); });
                    KeyValuePair<GoapKey, bool> updated = new(change.Key, change.Value);
                    state.Add(updated);
                }
                // if it does not exist in the current state, add it
                else
                {
                    state.Add(new(change.Key, change.Value));
                }
            }
            return state;
        }

        /**
		 * Used for building up the graph and holding the running costs of actions.
		 */

        private class Node
        {
            public readonly Node? parent;
            public readonly float runningCost;
            public readonly HashSet<KeyValuePair<GoapKey, bool>> state;
            public readonly GoapGoal? action;

            public Node(Node? parent, float runningCost, HashSet<KeyValuePair<GoapKey, bool>> state, GoapGoal? action)
            {
                this.parent = parent;
                this.runningCost = runningCost;
                this.state = state;
                this.action = action;
            }
        }
    }
}