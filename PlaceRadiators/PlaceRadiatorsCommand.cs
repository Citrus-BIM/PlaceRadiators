using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PlaceRadiators
{
    [Transaction(TransactionMode.Manual)]
    internal class PlaceRadiatorsCommand : IExternalCommand
    {
        private static long GetElementIdValue(ElementId id)
        {
#if REVIT_2025 || REVIT_2026 || REVIT_2027
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try { _ = GetPluginStartInfo(); } catch { }

            var uiDoc = commandData.Application.ActiveUIDocument;
            var doc = uiDoc.Document;
            var sel = uiDoc.Selection;

            // 1) Выбираем окна в связях (каждый пик -> окно + ссылка на RevitLinkInstance)
            List<LinkedWindowPick> picked;
            try
            {
                var refs = sel.PickObjects(
                    ObjectType.LinkedElement,
                    new WindowsOnlyInLinksFilter(doc),
                    "Выберите окна в связанных файлах"
                );

                picked = refs.Select(r =>
                {
                    var li = doc.GetElement(r.ElementId) as RevitLinkInstance;
                    var ld = li?.GetLinkDocument();
                    var fi = ld?.GetElement(r.LinkedElementId) as FamilyInstance;

                    return new LinkedWindowPick
                    {
                        LinkId = li?.Id,
                        WindowId = fi?.Id
                    };
                })
                .Where(x => x.LinkId != null && x.WindowId != null)
                .GroupBy(x => (Link: GetElementIdValue(x.LinkId!), Elem: GetElementIdValue(x.WindowId!)))
                .Select(g => g.First())
                .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (picked.Count == 0)
            {
                TaskDialog.Show("Revit", "Окна не выбраны.");
                return Result.Cancelled;
            }

            // 2) Сервисные коллекции
            var hostLevels = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .Cast<Level>()
                .ToList();

            // параметры типа ОКНА (берем из первого выбранного окна в его linkDoc)
            var firstWindow = GetLinkedWindow(doc, picked.First(), out _, out _);
            if (firstWindow == null)
            {
                TaskDialog.Show("Revit", "Выбранные окна больше недоступны. Проверьте, что связанные файлы загружены.");
                return Result.Cancelled;
            }

            var firstWindowTypeParams = firstWindow.Symbol.Parameters
                .Cast<Parameter>()
                .Where(p => p.StorageType == StorageType.Double)
                .Select(p => p.Definition.Name)
                .Distinct()
                .OrderBy(p => p, new AlphanumComparatorFastString())
                .ToList();

            // Семейства МЭО — напрямую по Family (быстро)
            var mechanicalFamilies = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(f => f.FamilyCategory?.Id != null && GetElementIdValue(f.FamilyCategory.Id) == (long)BuiltInCategory.OST_MechanicalEquipment)
                .OrderBy(f => f.Name, new AlphanumComparatorFastString())
                .ToList();

            // 3) UI
            var dlg = new PlaceRadiatorsWPF(doc, firstWindowTypeParams, mechanicalFamilies);
            dlg.ShowDialog();
            if (dlg.DialogResult != true) return Result.Cancelled;

            var windowWidthParamName = dlg.SelectedWindowWidthParameter; // параметр ширины окна (типовой)
            var radiatorBaseType = dlg.SelectedRadiatorType;         // выбранный тип радиатора
            var radiatorWidthParamName = dlg.SelectedRadiatorWidthParameter;
            var byType = dlg.RadiatorWidthByButtonName == "radioButton_Type";
            var radiatorThicknessParamName = dlg.SelectedRadiatorThicknessParameter;

            int percent = 50;
            int.TryParse(dlg.PercentageLength, out percent);
            if (percent <= 0) percent = 50;

            double indentFromLevelFt = MmToFt(double.TryParse(dlg.IndentFromLevel, out var mm1) ? mm1 : 100);
            double indentFromWallFt = MmToFt(double.TryParse(dlg.IndentFromWall, out var mm2) ? mm2 : 100);

            // 4) Размещение
            var activated = new HashSet<ElementId>();

            using (var tg = new TransactionGroup(doc, "Расставить радиаторы"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Создание радиаторов"))
                {
                    t.Start();

                    foreach (var pick in picked)
                    {
                        var window = GetLinkedWindow(doc, pick, out var linkDoc, out var transform);
                        if (window == null || linkDoc == null || transform == null) continue;

                        var windowLocation = window.Location as LocationPoint;
                        if (windowLocation == null) continue;

                        // длина радиатора = ширина окна (тип) * %
                        var windowWidthParam = GetParameterByName(window.Symbol, windowWidthParamName);
                        if (windowWidthParam == null) continue;

                        double baseW = windowWidthParam.AsDouble();
                        double targetLen = RoundUpToIncrementMmFeet(baseW * percent / 100.0, 100); // шаг 100 мм

                        // подбираем/создаём тип
                        FamilySymbol useType = radiatorBaseType;

                        if (byType)
                        {
                            var found = new FilteredElementCollector(doc)
                                .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                                .WhereElementIsElementType()
                                .Cast<FamilySymbol>()
                                .Where(fs => fs.Family.Id == radiatorBaseType.Family.Id)
                                .Where(fs =>
                                {
                                    var pT = GetParameterByName(fs, radiatorThicknessParamName);
                                    var pL = GetParameterByName(fs, radiatorWidthParamName);
                                    if (pT == null || pL == null) return false;

                                    var baseThicknessParam = GetParameterByName(radiatorBaseType, radiatorThicknessParamName);
                                    if (baseThicknessParam == null) return false;

                                    double baseT = baseThicknessParam.AsDouble();
                                    return Almost(pT.AsDouble(), baseT) && Almost(pL.AsDouble(), targetLen);
                                })
                                .FirstOrDefault();

                            if (found == null)
                            {
                                var name = $"{radiatorBaseType.Name} L={Math.Round(FtToMm(targetLen))}";
                                useType = (FamilySymbol)radiatorBaseType.Duplicate(name);
                                var widthParam = GetParameterByName(useType, radiatorWidthParamName);
                                if (widthParam != null && !widthParam.IsReadOnly) widthParam.Set(targetLen);
                            }
                            else
                            {
                                useType = found;
                            }
                        }

                        if (!activated.Contains(useType.Id))
                        {
                            useType.Activate();
                            activated.Add(useType.Id);
                            doc.Regenerate();
                        }

                        // координаты окна -> в систему хоста
                        XYZ winPtHost = transform.OfPoint(windowLocation.Point);

                        // целевой фасинг
                        bool invert = false;
                        XYZ targetFacing = TargetFacingByWindow(window, transform, invert);

                        // ближайший уровень хоста
                        Level hostLevel = GetClosestHostLevel(hostLevels, linkDoc, window, transform);
                        if (hostLevel == null) continue;

                        // итоговая точка: Z = уровень + отступ
                        XYZ place = new XYZ(winPtHost.X, winPtHost.Y, hostLevel.Elevation + indentFromLevelFt);

                        // ширина стены берется до создания/поворота радиатора, чтобы не держаться за устаревшие API-объекты
                        double hostWallWidth = 0;
                        if (window.Host is Wall lw) hostWallWidth = lw.Width;

                        // создаём
                        var rad = doc.Create.NewFamilyInstance(place, useType, hostLevel, StructuralType.NonStructural);
                        var radId = rad.Id;

                        // ЖЁСТКО фиксируем смещение
                        var offsetParam =
                            rad.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM) ??
                            rad.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM);

                        if (offsetParam != null && !offsetParam.IsReadOnly)
                        {
                            offsetParam.Set(indentFromLevelFt);
                        }

                        // длина по экземпляру
                        if (!byType && !string.IsNullOrWhiteSpace(radiatorWidthParamName))
                        {
                            var p = rad.LookupParameter(radiatorWidthParamName);
                            if (p != null && !p.IsReadOnly) p.Set(targetLen);
                        }

                        // поворот
                        if (doc.GetElement(radId) is FamilyInstance createdRadiator && createdRadiator.IsValidObject)
                        {
                            OrientRadiatorFacing(doc, radId, place, createdRadiator.FacingOrientation, targetFacing);
                        }

                        ElementTransformUtils.MoveElement(
                            doc,
                            radId,
                            (hostWallWidth / 2.0 + indentFromWallFt) * targetFacing.Negate()
                        );
                    }

                    t.Commit();
                }

                tg.Assimilate();
            }

            return Result.Succeeded;
        }

        // ================= helpers =================

#if R2019 || R2020
        private static double MmToFt(double mm)
            => UnitUtils.ConvertToInternalUnits(mm, DisplayUnitType.DUT_MILLIMETERS);

        private static double FtToMm(double ft)
            => UnitUtils.ConvertFromInternalUnits(ft, DisplayUnitType.DUT_MILLIMETERS);
#else
        private static double MmToFt(double mm)
            => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);

        private static double FtToMm(double ft)
            => UnitUtils.ConvertFromInternalUnits(ft, UnitTypeId.Millimeters);
#endif

        private static bool Almost(double a, double b, double tol = 1e-6) => Math.Abs(a - b) < tol;

        private static Parameter GetParameterByName(Element element, string parameterName)
        {
            if (element == null || string.IsNullOrWhiteSpace(parameterName)) return null;

            return element.Parameters
                .Cast<Parameter>()
                .FirstOrDefault(p => p.Definition?.Name == parameterName);
        }

        private static FamilyInstance? GetLinkedWindow(
            Document doc,
            LinkedWindowPick pick,
            out Document? linkDoc,
            out Transform? transform)
        {
            linkDoc = null;
            transform = null;

            if (pick?.LinkId == null || pick.WindowId == null) return null;

            var link = doc.GetElement(pick.LinkId) as RevitLinkInstance;
            if (link == null || !link.IsValidObject) return null;

            linkDoc = link.GetLinkDocument();
            if (linkDoc == null) return null;

            var window = linkDoc.GetElement(pick.WindowId) as FamilyInstance;
            if (window == null || !window.IsValidObject) return null;

            transform = link.GetTotalTransform();
            return transform == null ? null : window;
        }

        private static double RoundUpToIncrementMmFeet(double valueFeet, double incMm)
        {
            double mm = FtToMm(valueFeet);
            mm = Math.Ceiling(mm / incMm) * incMm;
            return MmToFt(mm);
        }

        // Целевой фасинг строго по окну (с учётом зеркала); invert=true — развернуть наоборот
        private static XYZ TargetFacingByWindow(FamilyInstance window, Transform t, bool invert)
        {
            XYZ f = t.OfVector(window.FacingOrientation);
            if (t.HasReflection) f = f.Negate();                 // зеркальные связи
            f = new XYZ(f.X, f.Y, 0).Normalize();                 // строго по XY
            if (invert) f = f.Negate();                           // нужно «как у тебя» — противоположно окну
            return f;
        }

        // знаковый угол вокруг Z ([-PI..PI])
        private static double SignedAngleAroundZ(XYZ from, XYZ to)
        {
            var f = new XYZ(from.X, from.Y, 0).Normalize();
            var t = new XYZ(to.X, to.Y, 0).Normalize();
            double dot = Math.Max(-1.0, Math.Min(1.0, f.DotProduct(t)));
            double ang = Math.Acos(dot);
            double sign = Math.Sign(f.X * t.Y - f.Y * t.X);
            return ang * sign;
        }

        // ближайший уровень хоста к уровню окна в линке (учитывает Transform)
        private static Level GetClosestHostLevel(
            List<Level> hostLvls,
            Document linkDoc,
            FamilyInstance window,
            Transform t)
        {
            var lp = window.Location as LocationPoint;
            if (lp == null || t == null || hostLvls == null || hostLvls.Count == 0)
                return hostLvls?.FirstOrDefault();

            // высота окна в системе координат хоста
            double zHost = t.OfPoint(lp.Point).Z;

            const double tol = 0.001; // ~0.3 мм

            // 1) сначала ищем уровень, который <= высоты окна (то есть «этажа ниже»)
            var belowOrEqual = hostLvls
                .Where(l => l.Elevation <= zHost + tol)
                .OrderByDescending(l => l.Elevation)
                .FirstOrDefault();

            if (belowOrEqual != null)
                return belowOrEqual;

            // 2) если все уровни выше (подвалы/нестандартные случаи) — берём просто ближайший
            return hostLvls
                .OrderBy(l => Math.Abs(l.Elevation - zHost))
                .FirstOrDefault();
        }

        // поворот радиатора к целевому фасингу с авто-фиксом «задом наперёд»
        private static void OrientRadiatorFacing(Document doc, ElementId id, XYZ placePoint, XYZ currentFacing, XYZ targetFacing)
        {
            // повернуть со знаком
            double ang = SignedAngleAroundZ(currentFacing, targetFacing);
            if (Math.Abs(ang) > 1e-6)
            {
                var axis = Line.CreateBound(placePoint, placePoint + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(doc, id, axis, ang);
            }

            // если всё ещё «назад» — докрутить (или FlipFacing)
            if (doc.GetElement(id) is FamilyInstance e)
            {
                var now = new XYZ(e.FacingOrientation.X, e.FacingOrientation.Y, 0).Normalize();
                var trg = new XYZ(targetFacing.X, targetFacing.Y, 0).Normalize();

                if (now.DotProduct(trg) < 0)
                {
                    try { e.flipFacing(); }             
                    catch
                    {
                        var axis = Line.CreateBound(placePoint, placePoint + XYZ.BasisZ);
                        ElementTransformUtils.RotateElement(doc, id, axis, Math.PI);
                    }
                }
            }
        }

        // ================= телеметрия =================

        private static async Task GetPluginStartInfo()
        {
            Assembly thisAssembly = Assembly.GetExecutingAssembly();
            string assemblyName = "PlaceRadiators";
            string assemblyNameRus = "Расставить радиаторы";
            string assemblyFolderPath = Path.GetDirectoryName(thisAssembly.Location) ?? "";
            int lastBackslashIndex = assemblyFolderPath.LastIndexOf("\\");
            string dllPath = assemblyFolderPath.Substring(0, lastBackslashIndex + 1) + "PluginInfoCollector\\PluginInfoCollector.dll";

            try
            {
                Assembly assembly = Assembly.LoadFrom(dllPath);
                Type type = assembly.GetType("PluginInfoCollector.InfoCollector");
                if (type != null)
                {
                    object instance = Activator.CreateInstance(type);
                    var method = type.GetMethod("CollectPluginUsageAsync");
                    if (method != null)
                    {
                        Task task = (Task)method.Invoke(instance, new object[] { assemblyName, assemblyNameRus });
                        await task;
                    }
                }
            }
            catch { /* тихо игнорим */ }
        }
    }
}
