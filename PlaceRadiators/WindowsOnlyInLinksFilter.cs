using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

public class WindowsOnlyInLinksFilter : ISelectionFilter
{
    private readonly Document _doc;
    public WindowsOnlyInLinksFilter(Document doc) => _doc = doc;

    private static long GetElementIdValue(ElementId id)
    {
#if REVIT_2025 || REVIT_2026 || REVIT_2027
        return id.Value;
#else
        return id.IntegerValue;
#endif
    }

    public bool AllowElement(Element elem)
    {
        // Разрешаем кликать только инстансы связей (для ObjectType.LinkedElement)
        return elem is RevitLinkInstance;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        var hostElem = _doc.GetElement(reference);
        if (hostElem is RevitLinkInstance li)
        {
            var linkDoc = li.GetLinkDocument();
            if (linkDoc == null) return false;

            var linked = linkDoc.GetElement(reference.LinkedElementId) as FamilyInstance;
            if (linked == null) return false;

            // верхнеуровневое окно, не in-place, с хостом (чтобы отсечь створки и пр.)
            bool isWindow = linked.Category?.Id != null && GetElementIdValue(linked.Category.Id) == (long)BuiltInCategory.OST_Windows;
            bool topLevel = linked.SuperComponent == null;
            bool notInPlace = linked.Symbol?.Family?.IsInPlace != true;
            bool hasHost = linked.Host != null || linked.HostFace != null;

            return isWindow && topLevel && notInPlace && hasHost;
        }
        return false;
    }
}
