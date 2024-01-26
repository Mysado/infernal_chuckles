﻿using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Sisus.Init
{
    /// <summary>
    /// Represents an object that can <see cref="ICoroutinesExtensions.StartCoroutine">start</see>
    /// and <see cref="ICoroutinesExtensions.StopCoroutine">stop</see> <see cref="Coroutine">coroutines</see>.
    /// </summary>
    public interface ICoroutines
    {
        /// <summary>
        /// Gets or sets the object which <see langword="this"/> object can use to start or stop coroutines.
        /// </summary>
        [MaybeNull]
        ICoroutineRunner CoroutineRunner { get; set; }
    }
}