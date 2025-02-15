﻿using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BBI.JD
{
    public class RequestHandler : IExternalEventHandler
    {
        private Request request = new Request();
        private Family family;
        private int index = -1;
        private List<Element> elements;
        private CentroidVolume cv;
        private Dictionary<ElementId, ElementId> instanceIds = new Dictionary<ElementId, ElementId>();

        public Request Request
        {
            get { return request; }
        }

        public String GetName()
        {
            return "Center Gravity";
        }

        public int Index
        {
            get { return index; }
        }

        public List<Element> Elements
        {
            get { return elements; }
        }

        public CentroidVolume CV
        {
            get { return cv; }
        }

        public int ChangeIndex(int value)
        {
            index += value;

            return index;
        }

        public void ClearElements()
        {
            elements.Clear();
        }

        public void Execute(UIApplication application)
        {
            try
            {
                switch (Request.Take())
                {
                    case RequestId.None:
                        {
                            return;
                        }
                    case RequestId.CenterGravityFamily:
                        {
                            CenterGravityFamily(application);
                            break;
                        }
                    case RequestId.Select:
                        {
                            SelectionChanged(application);
                            break;
                        }
                    case RequestId.Update:
                        {
                            UpdateValues(application);
                            break;
                        }
                    case RequestId.VisualizeCenterGravity:
                        {
                            VisualizeCentroid(application);
                            break;
                        }
                    case RequestId.RemoveCenterGravity:
                        {
                            RemoveCenterGravityPoints(application);
                            break;
                        }
                }
            }
            catch (Exception ex){
                CrtlApplication.thisApp.ShowFormMessageError(ex);
            }
        }

        private void CenterGravityFamily(UIApplication application)
        {
            UIDocument uiDoc = application.ActiveUIDocument;
            Document document = uiDoc.Document;

            FilteredElementCollector families = new FilteredElementCollector(document)
                                        .OfClass(typeof(Family));

            family = families.FirstOrDefault<Element>
                (e => e.Name == "CenterGravityFamily") as Family;

            if (family == null)
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string folder = new FileInfo(assemblyPath).Directory.FullName;

                // Load Center Gravity Family
                using (Transaction transaction = new Transaction(document))
                {
                    transaction.Start("Load CenterGravityFamily");

                    document.LoadFamily(string.Concat(folder, "/../Resources/CenterGravityFamily.rfa"), out family);

                    transaction.Commit();
                }
            }
        }

        private void SelectionChanged(UIApplication application)
        {
            UIDocument uiDoc = application.ActiveUIDocument;
            Document document = uiDoc.Document;

            // Reset INDEX and ELEMENTS
            index = -1;
            elements = new List<Element>();

            var ids = uiDoc.Selection.GetElementIds();

            if (ids.Count > 0)
            {
                elements = new FilteredElementCollector(document, ids)
                    .WhereElementIsNotElementType()
                        .Where(e => e.IsPhysicalElement())
                            .ToList();

                index = 0;

                UpdateCentroidValues(application);
            }

            UpdateValues(application);
        }

        private void UpdateValues(UIApplication application)
        {
            CrtlApplication.thisApp.UpdateFormValues();
        }

        private void UpdateCentroidValues(UIApplication application)
        {
            VisualizeCentroid(application);

            CrtlApplication.thisApp.UpdateFormCentroidValues();
        }

        private void VisualizeCentroid(UIApplication application)
        {
            UIDocument uiDoc = application.ActiveUIDocument;
            Document document = uiDoc.Document;

            if (elements.Count > 0)
            {
                Element element = elements[0];

                cv = GeometryUtils.GetCentroid(elements, new Options());

                // Put graphical point
                FamilySymbol familySymbol = null;

                foreach (ElementId fsids in family.GetFamilySymbolIds())
                {
                    familySymbol = document.GetElement(fsids) as FamilySymbol;
                }

                if (familySymbol != null && familySymbol.FamilyName == "CenterGravityFamily")
                {
                    using (Transaction transaction = new Transaction(document))
                    {
                        transaction.Start("Put graphical Center Gravity point");

                        if (!familySymbol.IsActive)
                        {
                            familySymbol.Activate();
                        }

                        Level level = document.GetElement(element.LevelId) as Level;

                        FamilyInstance familyInstance = document.Create.NewFamilyInstance(cv.Centroid, familySymbol, new XYZ(0, 0, 0), level, StructuralType.NonStructural);

                        #if RVT2019
                            familyInstance.LookupParameter("CenterGravity").Set(cv.XYZToString(
                                document.GetUnits().GetFormatOptions(UnitType.UT_Length)
                            ));
                        #else
                            familyInstance.LookupParameter("CenterGravity").Set(cv.XYZToString(
                                document.GetUnits().GetFormatOptions(SpecTypeId.Length)
                            ));
                        #endif

                        instanceIds.Add(element.Id, familyInstance.Id);

                        transaction.Commit();
                    }
                }
            }
        }

        private void RemoveCenterGravityPoints(UIApplication application)
        {
            UIDocument uiDoc = application.ActiveUIDocument;
            Document document = uiDoc.Document;

            using (Transaction transaction = new Transaction(document))
            {
                transaction.Start("Remove Center Gravity points");

                foreach (ElementId id in instanceIds.Values)
                {
                    document.Delete(id);
                }

                transaction.Commit();
            }

            instanceIds = new Dictionary<ElementId, ElementId>();
        }
    }
}
