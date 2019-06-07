using System.Collections.Generic;
using UnityEngine;

namespace NormandErwan.DocFxForUnity
{
    /// <summary>
    /// The player of the game.
    /// </summary>
    public class Player : MonoBehaviour
    {
        [SerializeField]
        private List<string> equipment;

        [SerializeField]
        private int startingHealth;

        /// <summary>
        /// Gets the list of the equipment carried by the player.
        /// </summary>
        public List<string> Equipment { get { return equipment; } }

        /// <summary>
        /// Gets the current health of the player.
        /// </summary>
        public int Health { get; private set; }

        /// <summary>
        /// Gets the starting health of the player.
        /// </summary>
        public int StartingHealth { get { return startingHealth; } }

        /// <summary>
        /// Sets <see cref="Health"/> with <see cref="StartingHealth"/>.
        /// </summary>
        protected virtual void Start()
        {
            Health = StartingHealth;
        }

        /// <summary>
        /// Deacreases <see cref="Health"/> by a specified value and display a game over if <see cref="Health"/> drops
        /// to zero.
        /// </summary>
        /// <param name="value">How much to deacrease <see cref="Health"/>.</param>
        public void Hit(int value)
        {
            Health -= value;

            if (Health <= 0)
            {
                Debug.Log("GAME OVER");
            }
        }
    }
}