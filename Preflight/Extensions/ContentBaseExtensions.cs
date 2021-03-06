﻿using Preflight.Constants;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;

namespace Preflight.Extensions
{
    public static class ContentBaseExtensions
    {
        public static IEnumerable<Property> GetPreflightProperties(this IContentBase content)
        {
            return content.Properties
                .Where(p => p.PropertyType.PropertyEditorAlias == KnownPropertyAlias.Grid ||
                            p.PropertyType.PropertyEditorAlias == KnownPropertyAlias.NestedContent ||
                            p.PropertyType.PropertyEditorAlias == KnownPropertyAlias.Textbox ||
                            p.PropertyType.PropertyEditorAlias == KnownPropertyAlias.Textarea ||
                            p.PropertyType.PropertyEditorAlias == KnownPropertyAlias.Rte);
        }
    }
}
