using System;
using Mmzkworks.muValidation;
using UnityEngine;

namespace Mmzkworks.muEventHub
{
    /// <summary>
    /// The GameObject must have a Collider or a Rigidbody, or Unity never calls its OnTrigger* / OnCollision*.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    internal sealed class ReceivesPhysicsCallbacksAttribute : ValidationAttribute
    {
        public override void Validate(ValidationContext context)
        {
            var go = context.GameObject;
            if (go == null) return;
            if (go.GetComponent<Collider>() != null || go.GetComponent<Rigidbody>() != null) return;
            context.Report("Requires a Collider or Rigidbody on the same GameObject to receive contacts");
        }
    }
}
