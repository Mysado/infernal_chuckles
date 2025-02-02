﻿using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Sisus.Init
{
    /// <summary>
    /// Represents a coroutine that has been started running in the editor.
    /// <para>
    /// Also offers static methods for <see cref="Start">starting</see> and <see cref="Stop">stopping</see> coroutines.
    /// </para>
    /// </summary>
    public sealed class EditorCoroutine : YieldInstruction
    {
        private static readonly List<EditorCoroutine> running = new List<EditorCoroutine>();
        private static readonly FieldInfo waitForSecondsSeconds;

        private readonly Stack<object> yielding = new Stack<object>();
        private double waitUntil = 0d;

        public bool IsFinished => yielding.Count == 0;

        static EditorCoroutine()
        {
            waitForSecondsSeconds = typeof(WaitForSeconds).GetField("m_Seconds", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private EditorCoroutine(IEnumerator routine) => yielding.Push(routine);

        /// <summary>
        /// Starts the provided <paramref name="coroutine"/>.
        /// </summary>
        /// <param name="coroutine"> The coroutine to start. </param>
        /// <returns>
        /// A reference to the started <paramref name="coroutine"/>.
        /// <para>
        /// This reference can be passed to <see cref="Stop"/> to stop
        /// the execution of the coroutine.
        /// </para>
        /// </returns>
        public static EditorCoroutine Start(IEnumerator coroutine)
        {
            if(running.Count == 0)
            {
                EditorApplication.update += UpdateRunningCoroutines;
            }

            var editorCoroutine = new EditorCoroutine(coroutine);
            running.Add(editorCoroutine);
            return editorCoroutine;
        }

        /// <summary>
        /// Stops the <paramref name="coroutine"/> that is running in edit mode.
        /// </summary>
        /// <param name="coroutine">
        /// Reference to the editor <see cref="IEnumerator">coroutine</see> to stop.
        /// <para>
        /// This is the reference that was returned by <see cref="Start"/>
        /// when the coroutine was started.
        /// </para>
        /// </param>
        public static void Stop(EditorCoroutine coroutine)
        {
            running.Remove(coroutine);

            if(running.Count == 0)
            {
                EditorApplication.update -= UpdateRunningCoroutines;
            }
        }

        /// <summary>
        /// Stops the <paramref name="coroutine"/> that is running.
        /// <para>
        /// If <see langword="this"/> is an object wrapped by a <see cref="Wrapper{TObject}"/> then
        /// the <paramref name="coroutine"/> is started on the wrapper behaviour.
        /// </para>
        /// <para>
        /// If <see langword="this"/> is a <see cref="MonoBehaviour"/> then the <paramref name="coroutine"/>
        /// is started on the object directly.
        /// </para>
        /// <para>
        /// Otherwise the <paramref name="coroutine"/> is started on an <see cref="Updater"/> instance.
        /// </para>
        /// </summary>
        /// <param name="coroutine"> The <see cref="IEnumerator">coroutine</see> to stop. </param>
        public static void Stop(IEnumerator coroutine)
        {
            foreach(var editorCoroutine in running)
            {
                int counter = editorCoroutine.yielding.Count;

                foreach(var item in editorCoroutine.yielding)
                {
                    if(counter == 1)
                    {
                        if(item == coroutine)
                        {
                            Stop(editorCoroutine);
                            return;
                        }
                    }

                    counter--;
                }
            }
        }

        /// <summary>
        /// Stops all coroutines that have been started using <see cref="Start"/> that are currently still running.
        /// </summary>
        public static void StopAll()
        {
            running.Clear();
            EditorApplication.update -= UpdateRunningCoroutines;
        }

        /// <summary>
        /// Continuously advances all currently running coroutines to their
        /// next phases until all of them have reached the end.
        /// <para>
        /// Note that this locks the current thread until all running coroutines have fully finished.
        /// If any coroutine contains <see cref="CustomYieldInstruction">CustomYieldInstructions</see>
        /// that take a long time to finish (or never finish in edit mode) this can cause the editor
        /// to freeze for the same duration.
        /// </para>
        /// </summary>
        public static void FastForwardAll()
        {
            for(int i = running.Count - 1; i >= 0; i--)
            {
                running[i].FastForwardToEnd();
            }
        }

        /// <summary>
        /// Advances all currently running coroutine to their next phase.
        /// </summary>
        /// <param name="skipWaits">
        /// (Optional) If <see langword="true"/> then yield instructions
        /// <see cref="WaitForSeconds"/> and <see cref="WaitForSecondsRealtime"/> are skipped.
        /// </param>
        /// <returns> <see langword="true"/> if any coroutines are still running, <see langword="false"/> if all have finished. </returns>
        public static bool MoveAllNext(bool skipWaits = false)
        {
            for(int i = running.Count - 1; i >= 0; i--)
            {
                running[i].MoveNext(skipWaits);
            }

            return running.Count > 0;
        }

        private static void UpdateRunningCoroutines()
        {
            for(int i = running.Count - 1; i >= 0; i--)
            {
                try
                {
                    if(!running[i].MoveNext())
                    {
                        running.RemoveAt(i);
                    }
                }
                catch
                {
                    running.RemoveAt(i);
                    if(running.Count == 0)
                    {
                        EditorApplication.update -= UpdateRunningCoroutines;
                    }
                    throw;
                }
            }

            if(running.Count == 0)
            {
                EditorApplication.update -= UpdateRunningCoroutines;
            }
        }

        /// <summary>
        /// Advances the coroutine to the next phase.
        /// </summary>
        /// <param name="skipWaits">
        /// (Optional) If <see langword="true"/> then yield instructions
        /// <see cref="WaitForSeconds"/> and <see cref="WaitForSecondsRealtime"/> are skipped.
        /// </param>
        /// <returns> <see langword="true"/> if coroutine is still running, <see langword="false"/> if it has finished. </returns>
        private bool MoveNext(bool skipWaits = false)
        {
            if(EditorApplication.timeSinceStartup < waitUntil && !skipWaits)
            {
                return true;
            }

            if(yielding.Count == 0)
            {
                return false;
            }

            var current = yielding.Peek();

            if(current is IEnumerator enumerator)
            {
                if(!enumerator.MoveNext())
                {
                    yielding.Pop();
                    return yielding.Count > 0;
                }

                yielding.Push(enumerator.Current);
                return true;
            }

            if(current is CustomYieldInstruction yieldInstruction)
            {
                if(yieldInstruction.keepWaiting)
                {
                    return true;
                }

                yielding.Pop();
                return true;
            }

            if(current is WaitForSeconds waitForSeconds)
            {
                waitUntil = EditorApplication.timeSinceStartup + (float)waitForSecondsSeconds.GetValue(waitForSeconds);
                yielding.Pop();
                return true;
            }

            if(current is WaitForSecondsRealtime waitForSecondsRealtime)
            {
                waitUntil = EditorApplication.timeSinceStartup + waitForSecondsRealtime.waitTime;
                yielding.Pop();
                return true;
            }

            if(current is WaitForEndOfFrame || current is WaitForFixedUpdate)
            {
                yielding.Pop();
                return true;
            }

            if(current is AsyncOperation asyncOperation)
            {
                if(!asyncOperation.isDone)
                {
                    return false;
                }

                yielding.Pop();
                return yielding.Count > 0;
            }

            yielding.Pop();
            return yielding.Count > 0;
        }

        /// <summary>
        /// Continuously advances the coroutine to the next phase
        /// until it has reached the end.
        /// <para>
        /// Note that this locks the current thread until the coroutine has fully finished.
        /// If the coroutine contains <see cref="CustomYieldInstruction">CustomYieldInstructions</see>
        /// that take a long time to finish (or never finish in edit mode) this can cause the editor
        /// to freeze for the same duration.
        /// </para>
        /// </summary>
        public void FastForwardToEnd()
        {
            while(MoveNext(true));
            Stop(this);
        }
    }
}