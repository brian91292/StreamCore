using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace StreamCore
{
    public class SharedMonoBehaviour : MonoBehaviour
    {
        private static MonoBehaviour _instance = null;
        private static readonly object instLock = new object();
        /// <summary>
        /// Starts a coroutine from a shared MonoBehaviour object
        /// </summary>
        /// <param name="coroutine"></param>
        public static void StartCoroutine(IEnumerator coroutine)
        {
            lock (instLock)
            {
                if (_instance == null)
                {
                    _instance = new GameObject().AddComponent<SharedMonoBehaviour>();
                    GameObject.DontDestroyOnLoad(_instance.gameObject);
                }
                _instance.StartCoroutine(coroutine);
            }
        }
    }
}
