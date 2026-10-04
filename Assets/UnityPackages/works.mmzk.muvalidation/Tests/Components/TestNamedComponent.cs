using System.Collections.Generic;
using UnityEngine;

namespace Mmzkworks.muValidation.Tests
{
    public class TestNamedComponent : MonoBehaviour
    {
        [NotEmpty] public string label;
        [NotEmpty] public List<string> labels;
    }
}
