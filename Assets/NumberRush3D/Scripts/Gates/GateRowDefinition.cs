using System;
using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Gates
{
    /// <summary>One side of a gate row: what happens if the army runs through it.</summary>
    [Serializable]
    public struct GateChoice
    {
        public GateKind kind;

        [Tooltip("Operand of the operation: +5, -10, x2, /2 ...")]
        public int value;

        public GateChoice(GateKind choiceKind, int choiceValue)
        {
            kind = choiceKind;
            value = choiceValue;
        }

        /// <summary>True for subtract / divide gates (red).</summary>
        public bool IsNegative => kind == GateKind.Subtract || kind == GateKind.Divide;
    }

    /// <summary>Two gates side by side. The army can only pass through one of them.</summary>
    [Serializable]
    public struct GateRowDefinition
    {
        public GateChoice left;
        public GateChoice right;

        public GateRowDefinition(GateChoice leftChoice, GateChoice rightChoice)
        {
            left = leftChoice;
            right = rightChoice;
        }

        /// <summary>Builds a row with the negative gate on a random side.</summary>
        public static GateRowDefinition RandomSide(GateChoice positive, GateChoice negative)
        {
            return UnityEngine.Random.value < 0.5f
                ? new GateRowDefinition(negative, positive)
                : new GateRowDefinition(positive, negative);
        }
    }
}
