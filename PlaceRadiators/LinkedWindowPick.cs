using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlaceRadiators
{
    class LinkedWindowPick
    {
        public ElementId? LinkId { get; set; }
        public ElementId? WindowId { get; set; }
    }
}
