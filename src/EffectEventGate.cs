using System;
using System.Collections.Generic;

namespace COM3D2.Pregnancy.Plugin
{
    // One roll per character and execution of an effect-bearing script block.
    // No clock timeout: pausing between the three particle pulses must not add rolls.
    internal sealed class EffectEventGate
    {
        sealed class Scope
        {
            internal WeakReference Owner;
            internal readonly Dictionary<string, Position> Maids = new Dictionary<string, Position>();
        }

        sealed class Position
        {
            internal string File, Label;
            internal int Index;
        }

        readonly List<Scope> scopes = new List<Scope>();

        internal static bool IsConceptionEffect(string resource, string bone)
        {
            return string.Equals(resource, "Particle/pSeieki_naka", StringComparison.OrdinalIgnoreCase)
                && string.Equals(bone, "_IK_vagina", StringComparison.OrdinalIgnoreCase);
        }

        internal void Reset(object owner)
        {
            for (int i = scopes.Count - 1; i >= 0; i--)
            {
                object target = scopes[i].Owner.Target;
                if (target == null || ReferenceEquals(target, owner)) scopes.RemoveAt(i);
            }
        }

        internal bool TryAccept(object owner, string maidGuid, string file, string label, int index)
        {
            if (owner == null || string.IsNullOrEmpty(maidGuid) || string.IsNullOrEmpty(file) || index < 0)
                return false;

            Scope scope = null;
            for (int i = scopes.Count - 1; i >= 0; i--)
            {
                object target = scopes[i].Owner.Target;
                if (target == null) scopes.RemoveAt(i);
                else if (ReferenceEquals(target, owner)) scope = scopes[i];
            }
            if (scope == null)
            {
                scope = new Scope { Owner = new WeakReference(owner) };
                scopes.Add(scope);
            }

            Position last;
            bool accepted = !scope.Maids.TryGetValue(maidGuid, out last)
                || !string.Equals(last.File, file, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(last.Label, label, StringComparison.Ordinal)
                || index < last.Index;
            scope.Maids[maidGuid] = new Position { File = file, Label = label, Index = index };
            return accepted;
        }
    }
}
