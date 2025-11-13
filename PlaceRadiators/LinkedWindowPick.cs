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
        public RevitLinkInstance Link { get; set; }
        public Document LinkDoc { get; set; }
        public FamilyInstance Window { get; set; }
        public Transform Transform { get; set; }
    }
}
