using System;
using UnityEngine;

namespace SpookyWater
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class GroupAttribute : PropertyAttribute
    {
        public readonly string Name;

        public GroupAttribute(string name)
        {
            Name = name;
        }
    }
}
