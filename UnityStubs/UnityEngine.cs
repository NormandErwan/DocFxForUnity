// Minimal Unity type stubs for DocFX documentation generation.
// Outside Assets/ so Unity ignores this file at build time.
using System;

namespace UnityEngine
{
    public class Object {}
    public class Component : Object {}
    public class Behaviour : Component {}
    public class MonoBehaviour : Behaviour {}

    public class AudioClip : Object {}

    public class AudioSource : Behaviour
    {
        public void PlayOneShot(AudioClip clip) {}
    }

    public static class Debug
    {
        public static void Log(object message) {}
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeFieldAttribute : Attribute {}
}
