using System.Collections.Generic;

namespace Preflight.Models
{
    /// <summary>
    /// A simple class for passing single property from the backoffice into the content checker
    /// </summary>
    public class SimpleProperty
    {
        public string Name { get; set; }
        public string Editor { get; set; }
        public string Value { get; set; }
    }

    public class DirtyProperties
    {
        public int Id { get; set; }
        public string Culture { get; set; }
        public List<SimpleProperty> Properties { get; set; }
    }
}
