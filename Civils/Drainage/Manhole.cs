﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using JPP.Core;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly:CommandClass(typeof(JPP.Civils.Drainage.Manhole))]

namespace JPP.Civils.Drainage
{
    class Manhole
    {
        public int Diameter { get; set; }
        public float InvertLevel { get; set; }
        public float CoverLevel { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool SafetyChain { get; set; }
        public bool SafetyRail { get; set; }

        public int MinimumMinorBenching { get; set; }
        public int MinimumMajorBenching { get; set; }

        public Point3d IntersectionPoint { get; set; }
        public List<PipeConnection> IncomingPipes { get; set; }

        public PipeConnection outgoingConnection { get; set; }

        //Helper properties
        public int LargestInternalPipeDiameter
        {
            get
            {
                int largest = 0;
                foreach (PipeConnection pipeConnection in IncomingPipes)
                {
                    if (pipeConnection.Diameter > largest)
                    {
                        largest = pipeConnection.Diameter;
                    }
                }

                if (outgoingConnection.Diameter > largest)
                {
                    largest = outgoingConnection.Diameter;
                }

                return largest;
            }
        }

        public float DepthToSoffitLevel
        {
            get
            {
                return CoverLevel - (InvertLevel + LargestInternalPipeDiameter/1000);
            }
        }

        public int WallThickness
        {
            get
            {
                Dictionary<int, int> wallThicnkess = new Dictionary<int, int>();
                wallThicnkess.Add(900, 70);
                wallThicnkess.Add(1050, 80);
                wallThicnkess.Add(1200, 90);
                wallThicnkess.Add(1350, 95);
                wallThicnkess.Add(1500, 105);
                wallThicnkess.Add(1800, 115);
                wallThicnkess.Add(2100, 125);
                wallThicnkess.Add(2400, 140);
                wallThicnkess.Add(2700, 150);
                wallThicnkess.Add(3000, 165);

                return wallThicnkess[Diameter];
            }
        }

        public Manhole()
        {
            IncomingPipes = new List<PipeConnection>();
        }

        public void GeneratePlan(Point3d location)
        {
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Database acCurDb = acDoc.Database;

            //Sort pipe connections for ease of drawing
            var sortedPipes = from p in IncomingPipes orderby p.Angle ascending select p;


            DrainageNetwork.Current.Standard.VerifyManhole(this);

                                   
            using (Transaction tr = acCurDb.TransactionManager.StartTransaction())
            {
                Vector3d offset = location.GetAsVector().Subtract(IntersectionPoint.GetAsVector());

                //Calculate alternatce connection point
                Circle slopeCircle = new Circle(location, Vector3d.ZAxis, 450);
                Vector3d slopeIntersect = location.GetVectorTo(outgoingConnection.Location.Add(offset));
                slopeIntersect = slopeIntersect * 450 / slopeIntersect.Length;
                Point2d slopePoint2D = new Point2d(location.Add(slopeIntersect).X, location.Add(slopeIntersect).Y);

                // Open the Block table for read
                BlockTable acBlkTbl;
                acBlkTbl = tr.GetObject(acCurDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                // Open the Block table record Model space for write
                BlockTableRecord acBlkTblRec;
                acBlkTblRec = tr.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                Core.Utilities.CreateLayer(Constants.JPP_D_PipeWalls, Constants.JPP_D_PipeWallColor);
                Core.Utilities.CreateLayer(Constants.JPP_D_PipeCentreline, Constants.JPP_D_PipeCentrelineColor, Constants.JPP_D_PipeCentrelineType);
                Core.Utilities.CreateLayer(Constants.JPP_D_ManholeWall, Constants.JPP_D_ManholeWallColor);

                Circle pipeIntrusion = new Circle(location, Vector3d.ZAxis, (double)(Diameter / 2) - 150f);

                //Outgoin line
                Polyline outgoingLine = new Polyline();
                outgoingLine.AddVertexAt(0, new Point2d(location.X, location.Y), 0, 0, 0);
                outgoingLine.AddVertexAt(1, new Point2d(outgoingConnection.Location.Add(offset).X, outgoingConnection.Location.Add(offset).Y), 0, 0, 0);
                outgoingLine.Layer = Constants.JPP_D_PipeCentreline;

                Polyline outgoingoffsetPlus = outgoingLine.GetOffsetCurves(outgoingConnection.Diameter / 2)[0] as Polyline;
                outgoingoffsetPlus.Layer = Constants.JPP_D_PipeWalls;
                Polyline outgoingoffsetMinus = outgoingLine.GetOffsetCurves(-outgoingConnection.Diameter / 2)[0] as Polyline;
                outgoingoffsetMinus.Layer = Constants.JPP_D_PipeWalls;

                //Add descriptive text
                MText outlabel = new MText();
                outlabel.Location = new Point3d(outgoingConnection.Location.Add(offset).X, outgoingConnection.Location.Add(offset).Y, 0);
                outlabel.Contents = outgoingConnection.Code + "\\P" + outgoingConnection.Diameter + "%%C";


                acBlkTblRec.AppendEntity(outlabel);
                tr.AddNewlyCreatedDBObject(outlabel, true);

                outlabel.TextHeight = 40;
                outlabel.AlignTo(outgoingLine);

                //Pipe walls
                Polyline outouteroffsetPlus = outgoingLine.GetOffsetCurves(outgoingConnection.Diameter / 2 + 20f)[0] as Polyline;
                outouteroffsetPlus.Layer = Constants.JPP_D_PipeWalls;
                Point3dCollection intersection = new Point3dCollection();
                outouteroffsetPlus.IntersectWith(pipeIntrusion, Intersect.ExtendArgument, intersection, IntPtr.Zero, IntPtr.Zero);
                outouteroffsetPlus.AddVertexAt(0, new Point2d(intersection[0].X, intersection[0].Y), 0, 0, 0);
                outouteroffsetPlus.RemoveVertexAt(1);

                Polyline outouteroffsetMinus = outgoingLine.GetOffsetCurves(-outgoingConnection.Diameter / 2 - 20f)[0] as Polyline;
                outouteroffsetMinus.Layer = Constants.JPP_D_PipeWalls;
                intersection = new Point3dCollection();
                outouteroffsetMinus.IntersectWith(pipeIntrusion, Intersect.ExtendArgument, intersection, IntPtr.Zero, IntPtr.Zero);
                outouteroffsetMinus.AddVertexAt(0, new Point2d(intersection[0].X, intersection[0].Y), 0, 0, 0);
                outouteroffsetMinus.RemoveVertexAt(1);

                Polyline outcloseWall = new Polyline();
                outcloseWall.AddVertexAt(0, new Point2d(outouteroffsetMinus.StartPoint.X, outouteroffsetMinus.StartPoint.Y), 0, 0, 0);
                outcloseWall.AddVertexAt(0, new Point2d(outouteroffsetPlus.StartPoint.X, outouteroffsetPlus.StartPoint.Y), 0, 0, 0);
                outcloseWall.Layer = Constants.JPP_D_PipeWalls;

                acBlkTblRec.AppendEntity(outgoingLine);
                tr.AddNewlyCreatedDBObject(outgoingLine, true);

                acBlkTblRec.AppendEntity(outgoingoffsetMinus);
                tr.AddNewlyCreatedDBObject(outgoingoffsetMinus, true);
                acBlkTblRec.AppendEntity(outgoingoffsetPlus);
                tr.AddNewlyCreatedDBObject(outgoingoffsetPlus, true);
                acBlkTblRec.AppendEntity(outouteroffsetMinus);
                tr.AddNewlyCreatedDBObject(outouteroffsetMinus, true);
                acBlkTblRec.AppendEntity(outouteroffsetPlus);
                tr.AddNewlyCreatedDBObject(outouteroffsetPlus, true);
                acBlkTblRec.AppendEntity(outcloseWall);
                tr.AddNewlyCreatedDBObject(outcloseWall, true);

                Polyline lastLine = outgoingoffsetPlus;                               

                for (int i = 0; i < sortedPipes.Count(); i++)
                {
                    PipeConnection pipeConnection = sortedPipes.ToArray()[i];

                    //Create centreline
                    Polyline newLine = new Polyline();//location, pipeConnection.Location.Add(offset)
                    newLine.AddVertexAt(0, new Point2d(location.X, location.Y), 0, 0, 0);
                    newLine.AddVertexAt(1, new Point2d(pipeConnection.Location.Add(offset).X, pipeConnection.Location.Add(offset).Y), 0, 0, 0);
                    newLine.Layer = Constants.JPP_D_PipeCentreline;

                    //Add descriptive text
                    MText label = new MText();                    
                    label.Location = new Point3d(pipeConnection.Location.Add(offset).X, pipeConnection.Location.Add(offset).Y, 0);
                    label.Contents = pipeConnection.Code + "\\P" + pipeConnection.Diameter + "%%C";


                    acBlkTblRec.AppendEntity(label);
                    tr.AddNewlyCreatedDBObject(label, true);

                    label.TextHeight = 40;
                    label.AlignTo(newLine);

                    //Pipe walls
                    Polyline outeroffsetPlus = newLine.GetOffsetCurves(pipeConnection.Diameter / 2 + 20f)[0] as Polyline;
                    outeroffsetPlus.Layer = Constants.JPP_D_PipeWalls;
                    intersection = new Point3dCollection();
                    outeroffsetPlus.IntersectWith(pipeIntrusion, Intersect.ExtendArgument, intersection, IntPtr.Zero, IntPtr.Zero);
                    outeroffsetPlus.AddVertexAt(0, new Point2d(intersection[0].X, intersection[0].Y), 0, 0, 0);
                    outeroffsetPlus.RemoveVertexAt(1);

                    Polyline outeroffsetMinus = newLine.GetOffsetCurves(-pipeConnection.Diameter / 2 - 20f)[0] as Polyline;
                    outeroffsetMinus.Layer = Constants.JPP_D_PipeWalls;
                    intersection = new Point3dCollection();
                    outeroffsetMinus.IntersectWith(pipeIntrusion, Intersect.ExtendArgument, intersection, IntPtr.Zero, IntPtr.Zero);
                    outeroffsetMinus.AddVertexAt(0, new Point2d(intersection[0].X, intersection[0].Y), 0, 0, 0);
                    outeroffsetMinus.RemoveVertexAt(1);

                    Polyline closeWall = new Polyline();
                    closeWall.AddVertexAt(0, new Point2d(outeroffsetMinus.StartPoint.X, outeroffsetMinus.StartPoint.Y), 0, 0, 0);
                    closeWall.AddVertexAt(0, new Point2d(outeroffsetPlus.StartPoint.X, outeroffsetPlus.StartPoint.Y), 0, 0, 0);
                    closeWall.Layer = Constants.JPP_D_PipeWalls;


                    //Check that angle is ok
                    //Skip the checl if this is the only incoming pipe
                    if (sortedPipes.Count() > 1)
                    {
                        if (pipeConnection.Angle < 135 || pipeConnection.Angle > 225)
                        {
                            //Angle exceeds 45° so change
                            Point3dCollection slopeIntersectCollection = new Point3dCollection();
                            newLine.IntersectWith(slopeCircle, Intersect.ExtendArgument, slopeIntersectCollection, IntPtr.Zero, IntPtr.Zero);
                            Point3d circleIntersectPoint = slopeIntersectCollection[0];
                            newLine.AddVertexAt(1, new Point2d(circleIntersectPoint.X, circleIntersectPoint.Y), 0, 0, 0);

                            newLine.SetPointAt(0, slopePoint2D);
                        }
                    }

                    Polyline offsetPlus = newLine.GetOffsetCurves(pipeConnection.Diameter / 2)[0] as Polyline;
                    offsetPlus.Layer = Constants.JPP_D_PipeWalls;
                    Polyline offsetMinus = newLine.GetOffsetCurves(-pipeConnection.Diameter / 2)[0] as Polyline;
                    offsetMinus.Layer = Constants.JPP_D_PipeWalls;

                    //Fillet
                    Point3dCollection collection = new Point3dCollection();
                    offsetMinus.IntersectWith(lastLine, Intersect.ExtendBoth, collection, IntPtr.Zero, IntPtr.Zero);

                    //Check that the lines do intersect, may not if small pipe to large when parallel
                    if (collection.Count > 0)
                    {
                        Point3d Intersection = collection[0];
                        lastLine.SetPointAt(0, new Point2d(Intersection.X, Intersection.Y));
                        offsetMinus.SetPointAt(0, new Point2d(Intersection.X, Intersection.Y));
                    }

                    //Arc a = lastLine.Fillet(offsetMinus, 50);

                    acBlkTblRec.AppendEntity(newLine);
                    tr.AddNewlyCreatedDBObject(newLine, true);

                    acBlkTblRec.AppendEntity(offsetMinus);
                    tr.AddNewlyCreatedDBObject(offsetMinus, true);
                    acBlkTblRec.AppendEntity(offsetPlus);
                    tr.AddNewlyCreatedDBObject(offsetPlus, true);

                    acBlkTblRec.AppendEntity(outeroffsetMinus);
                    tr.AddNewlyCreatedDBObject(outeroffsetMinus, true);
                    acBlkTblRec.AppendEntity(outeroffsetPlus);
                    tr.AddNewlyCreatedDBObject(outeroffsetPlus, true);

                    acBlkTblRec.AppendEntity(closeWall);
                    tr.AddNewlyCreatedDBObject(closeWall, true);
                    /*acBlkTblRec.AppendEntity(a);
                    tr.AddNewlyCreatedDBObject(a, true);*/

                    lastLine = offsetPlus;

                }

                Point3dCollection lastCollection = new Point3dCollection();
                outgoingoffsetMinus.IntersectWith(lastLine, Intersect.ExtendBoth, lastCollection, IntPtr.Zero, IntPtr.Zero);

                if (lastCollection.Count > 0)
                {
                    Point3d lastIntersection = lastCollection[0]; //No intersection throwing error???
                    lastLine.SetPointAt(0, new Point2d(lastIntersection.X, lastIntersection.Y));
                    outgoingoffsetMinus.SetPointAt(0, new Point2d(lastIntersection.X, lastIntersection.Y));
                }

                //Create rings
                Circle innerManhole = new Circle(location, Vector3d.ZAxis, (double)(Diameter / 2));
                innerManhole.Layer = Constants.JPP_D_ManholeWall;
                acBlkTblRec.AppendEntity(innerManhole);
                tr.AddNewlyCreatedDBObject(innerManhole, true);                               

                //Calculate best location for steps and check minimum benching
                double maxAngle = 0;
                double previousAngle = 0;

                int segment = 0;                               

                for(int i = 0; i < sortedPipes.Count(); i++)
                {
                    PipeConnection pc = sortedPipes.ToArray()[i];

                    double anglebetween = pc.Angle - previousAngle;
                    previousAngle += pc.Angle;

                    if(anglebetween > maxAngle)
                    {
                        maxAngle = anglebetween;
                        segment = i;
                    }
                }

                PipeConnection edgeSegment = sortedPipes.ToArray()[segment];
                double stepCenter = edgeSegment.Angle - maxAngle / 2;

                //check the last segment is not better
                PipeConnection endSegment = sortedPipes.ToArray().Last();
                double lastAngle = 360 - endSegment.Angle;
                if(lastAngle > maxAngle)
                {
                    stepCenter = 360 - lastAngle / 2;
                }

                Polyline stepCenterLine = new Polyline();
                stepCenterLine.AddVertexAt(0, new Point2d(outgoingLine.StartPoint.X, outgoingLine.StartPoint.Y), 0, 0, 0);
                stepCenterLine.AddVertexAt(1, new Point2d(outgoingLine.EndPoint.X, outgoingLine.EndPoint.Y), 0, 0, 0);

                Matrix3d curUCSMatrix = acDoc.Editor.CurrentUserCoordinateSystem;
                CoordinateSystem3d curUCS = curUCSMatrix.CoordinateSystem3d;

                //Calculate both intersection points
                (Vector3d majorBenching, Vector3d minorBenching) = CalculateBenching(stepCenterLine, innerManhole);

                //ODO: Make sure this works and allow for pipe widths not centrelines
                if (minorBenching.Length < MinimumMinorBenching)
                {
                    Vector3d adjustment = minorBenching * (MinimumMinorBenching/minorBenching.Length);
                    innerManhole.Center = innerManhole.Center.Add(adjustment);
                    (majorBenching, minorBenching) = CalculateBenching(stepCenterLine, innerManhole);
                }

                if(majorBenching.Length < MinimumMajorBenching)
                {
                    if (minorBenching.Length > MinimumMinorBenching)
                    {
                        Vector3d adjustment = minorBenching * (MinimumMinorBenching / minorBenching.Length);
                        innerManhole.Center = innerManhole.Center.Add(adjustment);
                        (majorBenching, minorBenching) = CalculateBenching(stepCenterLine, innerManhole);

                        if (majorBenching.Length < MinimumMajorBenching)
                        {
                            throw new ArgumentException("Major benching does not meet minimum distances");
                        }
                    }

                    throw new ArgumentException("Major benching does not meet minimum distances");
                }

                //Create rings now offset is fixed
                Circle outerManhole = new Circle(location, Vector3d.ZAxis, (double)(Diameter / 2) + WallThickness);
                outerManhole.Layer = Constants.JPP_D_ManholeWall;
                acBlkTblRec.AppendEntity(outerManhole);
                tr.AddNewlyCreatedDBObject(outerManhole, true);

                Circle outerSurround = new Circle(location, Vector3d.ZAxis, (double)(Diameter / 2) + WallThickness + 100f);
                outerSurround.Layer = Constants.JPP_D_ManholeWall;
                acBlkTblRec.AppendEntity(outerSurround);
                tr.AddNewlyCreatedDBObject(outerSurround, true);

                // Rotate the polyline 45 degrees, around the Z-axis of the current UCS
                // using a base point of (4,4.25,0)
                stepCenterLine.TransformBy(Matrix3d.Rotation(stepCenter * Math.PI / 180, -curUCS.Zaxis, stepCenterLine.StartPoint));

                acBlkTblRec.AppendEntity(stepCenterLine);
                tr.AddNewlyCreatedDBObject(stepCenterLine, true);

                //stepCenterLine.TransformBy

                //Add safety features
                if(SafetyChain)
                {
                    throw new NotImplementedException();
                }
                if(SafetyRail)
                {
                    throw new NotImplementedException();
                }

                //Generate Table
                Table tb = new Table();
                tb.SetSize(15, 2);                
                tb.SetRowHeight(60);
                tb.Columns[0].Width = 1200;
                tb.Columns[1].Width = 400;
                tb.Position = new Point3d(location.X + 1700, location.Y + 450, 0);

                tb.SetTableRow("MANHOLE", this.Name.ToUpper(), 0);
                tb.SetTableRow("MANHOLE DIAMETER", this.Diameter.ToString(), 1);
                tb.SetTableRow("COVER LEVEL", this.CoverLevel.ToString(), 2);
                tb.SetTableRow("INVERT LEVEL", this.InvertLevel.ToString(), 3);
                tb.SetTableRow("MANHOLE TYPE", "", 4);
                tb.SetTableRow("DEPTH TO SOFFIT", this.DepthToSoffitLevel.ToString(), 5);
                tb.SetTableRow("DEPTH TO CUT OUT RECESS", "N/A", 6);
                tb.SetTableRow("COVER SIZE", "", 7);
                tb.SetTableRow("COVER SPEC", "TBC", 8);
                tb.SetTableRow("COVER DEPTH", "", 9);
                tb.SetTableRow("LADDER OR DOUBLE STEPS", "", 10);
                tb.SetTableRow("SAFETY CHAIN", this.SafetyChain.ToString().ToUpper(),11);
                tb.SetTableRow("SAFETY RAIL", this.SafetyRail.ToString().ToUpper(), 12);
                tb.SetTableRow("AMPS PLATFORM", "N/A", 13);                //TODO: Add proper amps check
                tb.SetTableRow("HOLE SIZE IN COVER SLAB", "", 14);

                tb.GenerateLayout();

                acBlkTblRec.AppendEntity(tb);
                tr.AddNewlyCreatedDBObject(tb, true);

                //Finalise
                tr.Commit();
            }
        }   
        
        public (Vector3d major, Vector3d minor) CalculateBenching(Polyline centerLine, Circle manhole)
        {
            Point3dCollection intersection = new Point3dCollection();
            centerLine.IntersectWith(manhole, Intersect.ExtendThis, intersection, IntPtr.Zero, IntPtr.Zero);
            List<Vector3d> benchingLenghts = new List<Vector3d>();
            foreach (Point3d p in intersection)
            {
                benchingLenghts.Add(p.GetVectorTo(manhole.Center));
            }

            Vector3d minorBenching, majorBenching;

            minorBenching = benchingLenghts.OrderBy(o => o.Length).First();
            majorBenching = benchingLenghts.OrderBy(o => o.Length).Last();

            return (majorBenching, minorBenching);
        }

        [CommandMethod("C_D_AddPlan")]
        public static void AddPlan()
        {
            Manhole current = new Manhole();

            Document acDoc = Application.DocumentManager.MdiActiveDocument;

            PromptStringOptions pStrOpts = new PromptStringOptions("\nEnter manhole diameter: ");
            pStrOpts.AllowSpaces = false;
            current.Diameter = int.Parse(acDoc.Editor.GetString(pStrOpts).StringResult);

            pStrOpts = new PromptStringOptions("\nEnter manhole cover level: ");
            pStrOpts.AllowSpaces = false;
            current.CoverLevel = float.Parse(acDoc.Editor.GetString(pStrOpts).StringResult);

            pStrOpts = new PromptStringOptions("\nEnter manhole invert level: ");
            pStrOpts.AllowSpaces = false;
            current.InvertLevel = float.Parse(acDoc.Editor.GetString(pStrOpts).StringResult);

            // Prompt for the start point
            PromptPointResult pPtRes;
            PromptPointOptions pPtOpts = new PromptPointOptions("");
            pPtOpts.Message = "\nPlease click the pipe intersection point: ";
            var temp3d = acDoc.Editor.GetPoint(pPtOpts).Value;
            current.IntersectionPoint = new Point3d(temp3d.X, temp3d.Y, 0);

            current.outgoingConnection = new PipeConnection();
            pPtOpts = new PromptPointOptions("");
            pPtOpts.Message = "\nPlease click the outgoing pipe intersection point: ";
            var outgoingLocationResult = acDoc.Editor.GetPoint(pPtOpts);
            if (outgoingLocationResult.Status != PromptStatus.OK)
            {
                throw new NotImplementedException();
            }
            temp3d = outgoingLocationResult.Value;
            current.outgoingConnection.Location = new Point3d(temp3d.X, temp3d.Y, 0);

            pStrOpts = new PromptStringOptions("\nEnter outgoing pipe diameter: ");
            pStrOpts.AllowSpaces = false;
            var outgoingDiameterResult = acDoc.Editor.GetString(pStrOpts);
            if (outgoingDiameterResult.Status != PromptStatus.OK)
            {
                throw new NotImplementedException();
            }
            current.outgoingConnection.Diameter = int.Parse(outgoingDiameterResult.StringResult);

            Vector3d outgoing = current.IntersectionPoint.GetVectorTo(current.outgoingConnection.Location);

            bool processing = true;
            while (processing)
            {
                PipeConnection pc = new PipeConnection();
                pPtOpts = new PromptPointOptions("");
                pPtOpts.Message = "\nPlease click the pipe intersection point, or press escape if done: ";
                var locationResult = acDoc.Editor.GetPoint(pPtOpts);
                if (locationResult.Status != PromptStatus.OK)
                {
                    processing = false;
                    break;
                }
                temp3d = locationResult.Value;
                pc.Location = new Point3d(temp3d.X, temp3d.Y, 0);

                pStrOpts = new PromptStringOptions("\nEnter pipe diameter: ");
                pStrOpts.AllowSpaces = false;
                var diameterResult = acDoc.Editor.GetString(pStrOpts);
                if (diameterResult.Status != PromptStatus.OK)
                {
                    processing = false;
                    break;
                }
                pc.Diameter = int.Parse(diameterResult.StringResult);

                Vector3d line = current.IntersectionPoint.GetVectorTo(pc.Location);
                pc.Angle = line.GetAngleTo(outgoing, Vector3d.ZAxis) * 180 / Math.PI;

                current.IncomingPipes.Add(pc);
            }

            pPtOpts = new PromptPointOptions("");
            pPtOpts.Message = "\nPlease click the location to insert plan detail: ";
            var planLocationResult = acDoc.Editor.GetPoint(pPtOpts);
            if (planLocationResult.Status != PromptStatus.OK)
            {
                throw new NotImplementedException();
            }
            temp3d = planLocationResult.Value;

            try
            {
                current.GeneratePlan(new Point3d(temp3d.X, temp3d.Y, 0));
            }
            catch (ArgumentException e)
            {
                MessageBox.Show(e.Message);
            }
        }

        [CommandMethod("C_D_ProcessSchedule")]
        public static void ProcessSchedule()
        {
            Point3d location = new Point3d();
            List<Manhole> manholes = new List<Manhole>();

            //Prompt for input file
            OpenFileDialog ofd = new OpenFileDialog("Select .csv file containing manhole schedule", null, "csv", "CSVFileToLink", OpenFileDialog.OpenFileDialogFlags.DoNotTransferRemoteFiles);
            System.Windows.Forms.DialogResult dr = ofd.ShowDialog();

            Matrix3d curUCSMatrix = Application.DocumentManager.MdiActiveDocument.Editor.CurrentUserCoordinateSystem;
            CoordinateSystem3d curUCS = curUCSMatrix.CoordinateSystem3d;

            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Database acCurDb = acDoc.Database;

            using (Transaction tr = acCurDb.TransactionManager.StartTransaction())
            {
                // Open the Block table for read
                BlockTable acBlkTbl;
                acBlkTbl = tr.GetObject(acCurDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                // Open the Block table record Model space for write
                BlockTableRecord acBlkTblRec;
                acBlkTblRec = tr.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                if (dr == System.Windows.Forms.DialogResult.OK)
            {
                    OpenFileDialog ofd2 = new OpenFileDialog("Select .csv file containing pipe schedule", null, "csv", "CSVPipeFileToLink", OpenFileDialog.OpenFileDialogFlags.DoNotTransferRemoteFiles);
                    System.Windows.Forms.DialogResult dr2 = ofd2.ShowDialog();

                    if (dr2 == System.Windows.Forms.DialogResult.OK)
                    {
                        Dictionary<string, float> InvertLevels = new Dictionary<string, float>();
                        Dictionary<string, float> CoverLevels = new Dictionary<string, float>();

                        using (StreamReader sr = File.OpenText(ofd2.Filename))
                        {
                            string line;
                            sr.ReadLine(); //discard headers

                            while (true)
                            {
                                line = sr.ReadLine();

                                if (line == null)
                                    break;

                                string[] columns = line.Split(',');

                                string id = columns[1];
                                InvertLevels.Add(id, float.Parse(columns[8]));
                                CoverLevels.Add(id, float.Parse(columns[10]));
                            }
                        }

                            using (StreamReader sr = File.OpenText(ofd.Filename))
                        {
                            string line;
                            sr.ReadLine(); //discard headers
                            while (true)
                            {
                                line = sr.ReadLine();

                                if (line == null)
                                    break;

                                string[] columns = line.Split(',');
                                Manhole m = new Manhole();

                                m.Name = columns[0];

                                if(InvertLevels.ContainsKey(m.Name))
                                {
                                    m.InvertLevel = InvertLevels[m.Name];
                                }
                                if (CoverLevels.ContainsKey(m.Name))
                                {
                                    m.CoverLevel = CoverLevels[m.Name];
                                }

                                m.Diameter = int.Parse(columns[1]);

                                Polyline outLine = new Polyline();
                                outLine.AddVertexAt(0, new Point2d(), 0, 0, 0);
                                outLine.AddVertexAt(1, new Point2d(0, -1500), 0, 0, 0);
                                /*acBlkTblRec.AppendEntity(outLine);
                                tr.AddNewlyCreatedDBObject(outLine, true);*/

                                m.outgoingConnection = new PipeConnection()
                                {
                                    Code = columns[4],
                                    Diameter = int.Parse(columns[6]),
                                    Location = outLine.EndPoint
                                };

                                int c = 8;
                                while (c < columns.Count())
                                {
                                    Polyline baseLine = new Polyline();
                                    baseLine.AddVertexAt(0, new Point2d(), 0, 0, 0);
                                    baseLine.AddVertexAt(1, new Point2d(0, -1500), 0, 0, 0);

                                    if (columns[c] == "")
                                        break; //If column is empty break loop

                                    PipeConnection incoming = new PipeConnection
                                    {
                                        Code = columns[c],
                                        Angle = double.Parse(columns[c + 2]),
                                        Diameter = int.Parse(columns[c + 3])
                                    };

                                    baseLine.TransformBy(Matrix3d.Rotation(incoming.Angle * Math.PI / 180, -curUCS.Zaxis, baseLine.StartPoint));
                                    /*acBlkTblRec.AppendEntity(baseLine);
                                    tr.AddNewlyCreatedDBObject(baseLine, true);*/
                                    incoming.Location = baseLine.EndPoint;

                                    m.IncomingPipes.Add(incoming);

                                    c += 5;
                                }

                                if (m.Diameter < 1200)
                                {
                                    Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("Manhole {0} ignored as non-adoptable due to diameter.\n", m.Name);
                                }
                                else
                                {
                                    manholes.Add(m);
                                }
                            }

                        }



                        foreach (Manhole m in manholes)
                        {
                            try
                            {
                                m.GeneratePlan(location);
                            }
                            catch (ArgumentException e)
                            {
                                acDoc.Editor.WriteMessage("Manhole {0} failed verification. {1}\n", m.Name, e.Message);
                            }                            
                            location = new Point3d(location.X + 5000, location.Y, 0);
                        }

                        tr.Commit();
                    }
                }
            }
        }
    }

    class PipeConnection
    {
        public int Diameter { get; set; }
        public Point3d Location { get; set; }
        public double Angle { get; set; }
        public string Code { get; set; }
    }
}
