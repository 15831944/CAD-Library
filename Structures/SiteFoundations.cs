﻿using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using JPP.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: CommandClass(typeof(JPP.CivilStructures.SiteFoundations))]

namespace JPP.CivilStructures
{
    public class SiteFoundations
    {
        /// <summary>
        /// Safe ground bearing pressure in kN/m2
        /// </summary>
        public int GroundBearingPressure { get; set; }

        /// <summary>
        /// Default width for all foundations
        /// </summary>
        public float DefaultWidth { get; set; }

        public List<NHBCTree> Trees;

        public float StartDepth;

        public float Step;

        public Shrinkage SoilShrinkage;

        public SiteFoundations()
        {
            Trees = new List<NHBCTree>();
            SoilShrinkage = Shrinkage.High;
        }

        private void GenerateTreeRings()
        {           
            StartDepth = 1;
            Step = 0.3f;
            int maxSteps = 0;

            // Get the current document and database
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Database acCurDb = acDoc.Database;

            Transaction acTrans = acCurDb.TransactionManager.TopTransaction;

            //Add the merged ring to the drawing
            // Open the Block table for read
            BlockTable acBlkTbl;
            acBlkTbl = acTrans.GetObject(acCurDb.BlockTableId, OpenMode.ForRead) as BlockTable;

            // Open the Block table record Model space for write
            BlockTableRecord acBlkTblRec;
            acBlkTblRec = acTrans.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

            List<DBObjectCollection> rings = new List<DBObjectCollection>();

            //Generate the rings for each tree
            foreach (NHBCTree tree in Trees)
            {
                DBObjectCollection collection = tree.DrawRings(SoilShrinkage, StartDepth, Step);
                if (collection.Count > maxSteps)
                {
                    maxSteps = collection.Count;
                }

                rings.Add(collection);
            }

            for (int ringIndex = 0; ringIndex < maxSteps; ringIndex++)
            {
                //Determine overlaps
                List<Curve> currentStep = new List<Curve>();
                DBObjectCollection splitCurves = new DBObjectCollection();

                //Build a collection of the outer rings only
                foreach (DBObjectCollection col in rings)
                {
                    //Check not stepping beyond
                    if (col.Count > ringIndex)
                    {
                        if (col[ringIndex] is Curve)
                        {
                            currentStep.Add(col[ringIndex] as Curve);
                        }
                    }
                }

                int nextGroup = 0;
                int[] groupings = new int[currentStep.Count];

                for (int currentCurveIndex = 0; currentCurveIndex < currentStep.Count; currentCurveIndex++)
                {
                    List<int> intersectionIndices = new List<int>();
                    for (int targetIndex = 0; targetIndex < currentStep.Count; targetIndex++)
                    {
                        //Make sure not testing against itself
                        if (currentCurveIndex != targetIndex)
                        {
                            Point3dCollection temp = new Point3dCollection();
                            currentStep[currentCurveIndex].IntersectWith(currentStep[targetIndex], Intersect.OnBothOperands, temp, new IntPtr(0), new IntPtr(0));
                            foreach (Point3d p in temp)
                            {
                                intersectionIndices.Add(targetIndex);
                            }
                        }
                    }

                    if (intersectionIndices.Count > 0)
                    {
                        int currentGroupId = groupings[intersectionIndices[0]];
                        for (int w = 1; w < intersectionIndices.Count; w++)
                        {
                            if (groupings[intersectionIndices[w]] != currentGroupId)
                            {
                                groupings[intersectionIndices[w]] = currentGroupId;
                            }
                        }
                        groupings[currentCurveIndex] = currentGroupId;
                    }
                    else
                    {
                        //No intersections found so stick in own group
                        groupings[currentCurveIndex] = nextGroup;
                        nextGroup++;
                    }
                }

                Dictionary<int, DBObjectCollection> curveGroups = new Dictionary<int, DBObjectCollection>();

                //Split groupings into collections
                for (int w = 0; w < groupings.Length; w++)
                {
                    int groupId = groupings[w];

                    if (!curveGroups.ContainsKey(groupId))
                    {
                        curveGroups.Add(groupId, new DBObjectCollection());
                    }

                    curveGroups[groupId].Add(currentStep[w]);
                }

                //Iterate over groups
                foreach (DBObjectCollection currentGroup in curveGroups.Values)
                {
                    List<Region> createdRegions = new List<Region>();

                    //Create regions
                    foreach (Curve c in currentGroup)
                    {
                        DBObjectCollection temp = new DBObjectCollection();
                        temp.Add(c);
                        DBObjectCollection regions = Region.CreateFromCurves(temp);
                        foreach (Region r in regions)
                        {
                            createdRegions.Add(r);
                        }
                    }

                    Region enclosed = createdRegions[0];

                    for (int i = 1; i < createdRegions.Count; i++)
                    {
                        enclosed.BooleanOperation(BooleanOperationType.BoolUnite, createdRegions[i]);
                    }

                    ObjectId regionId = acBlkTblRec.AppendEntity(enclosed);
                    acTrans.AddNewlyCreatedDBObject(enclosed, true);

                    Brep boundaryRep = new Brep(enclosed);
                    /*Point2dCollection boundaryLinePoints = new Point2dCollection();
                    foreach(var l in boundaryRep.Faces.First().Loops)
                    {
                        if(l.LoopType == LoopType.LoopExterior)
                        {
                            foreach (var vertex in l.Vertices)
                            {
                                boundaryLinePoints.Add(new Point2d(vertex.Point.X, vertex.Point.Y));
                            }
                        }
                    }

                    Polyline boundaryLine = new Polyline();
                    for(int p = 0; p < boundaryLinePoints.Count; p++)
                    {
                        boundaryLine.AddVertexAt(p, boundaryLinePoints[p], 0, 0, 0);
                    }

                    boundaryLine.Closed = true;

                    acBlkTblRec.AppendEntity(boundaryLine);
                    acTrans.AddNewlyCreatedDBObject(boundaryLine, true);*/
                    /*foreach (var e in boundaryRep.Edges)
                    {
                        CircularArc3d curve = (CircularArc3d)((ExternalCurve3d)e.Curve).NativeCurve;

                        Arc a = curve.GetArc();

                        acBlkTblRec.AppendEntity(a);
                        acTrans.AddNewlyCreatedDBObject(a, true); 
                    }*/
                    ObjectIdCollection temp2 = new ObjectIdCollection();
                    temp2.Add(regionId);

                    /*using (Hatch acHatch = new Hatch())
                    {
                        acBlkTblRec.AppendEntity(acHatch);
                        acTrans.AddNewlyCreatedDBObject(acHatch, true);

                        // Set the properties of the hatch object
                        // Associative must be set after the hatch object is appended to the 
                        // block table record and before AppendLoop
                        acHatch.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
                        acHatch.Associative = false;
                        acHatch.AppendLoop(HatchLoopTypes.Outermost, temp2);
                        acHatch.EvaluateHatch(true);
                    }*/

                }
            }

            /*

            foreach (Curve c in splitCurves)
            {
                acBlkTblRec.AppendEntity(c);
                acTrans.AddNewlyCreatedDBObject(c, true);
            }

            //Combine the rings, one stepping at a time
            for (int i = 0; i < maxSteps; i++)
            {
            }*/
        }


        [CommandMethod("CS_NewTree")]
        public static void NewPlot()
        {
            Document acDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Database acCurDb = acDoc.Database;

            NHBCTree newTree = new NHBCTree();

            //TODO: Add tree determination in here
            PromptStringOptions pStrOptsPlot = new PromptStringOptions("\nEnter tree height: ") { AllowSpaces = false };
            PromptResult pStrResPlot = acDoc.Editor.GetString(pStrOptsPlot);

            newTree.Height = float.Parse(pStrResPlot.StringResult);
            newTree.WaterDemand = WaterDemand.High;
            newTree.TreeType = TreeType.Deciduous;

            PromptPointOptions pPtOpts = new PromptPointOptions("\nEnter base point of the plot: ");
            PromptPointResult pPtRes = acDoc.Editor.GetPoint(pPtOpts);
            newTree.Location = new Autodesk.AutoCAD.Geometry.Point2d(pPtRes.Value.X, pPtRes.Value.Y);

            SiteFoundations sf = acDoc.GetDocumentStore<CivilStructureDocumentStore>().SiteFoundations;

            using (Transaction acTrans = acDoc.TransactionManager.StartTransaction())
            {
                sf.Trees.Add(newTree);
                sf.GenerateTreeRings();

                acTrans.Commit();
            }
        }
    }

    public enum Shrinkage
    {
        Low,
        Medium,
        High
    }
}
