﻿using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace JPP.Core
{
    public class DocumentStore
    {
        public static DocumentStore Current
        {
            get
            {
                if (_Current == null)
                {
                    _Current = new DocumentStore();
                }
                return _Current;
            }
        }

        static DocumentStore _Current;

        public DocumentStore()
        {
            //Load the data
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Database acCurDb = acDoc.Database;

            //Attach to current doc
            acDoc.BeginDocumentClose += AcDoc_BeginDocumentClose;
            acDoc.Database.BeginSave += Database_BeginSave;

            using (Transaction tr = acCurDb.TransactionManager.StartTransaction())
            {
                // Find the NOD in the database
                DBDictionary nod = (DBDictionary)tr.GetObject(acCurDb.NamedObjectsDictionaryId, OpenMode.ForWrite);

                Plots = new ObservableCollection<Plot>();
                if (nod.Contains("JPP_Plot"))
                {
                    ObjectId plotId = nod.GetAt("JPP_Plot");
                    Xrecord plotXRecord = (Xrecord)tr.GetObject(plotId, OpenMode.ForRead);
                    MemoryStream ms = new MemoryStream();
                    foreach (TypedValue value in plotXRecord.Data)
                    {
                        byte[] data = new byte[512];

                        string message = (string)value.Value;
                        //message = message.Replace('\a', '\0');
                        data = Encoding.ASCII.GetBytes(message);
                        ms.Write(data, 0, data.Length);
                    }
                    ms.Position = 0;
                    //System.Diagnostics.Debug.Print("===== OUR DATA: " + value.TypeCode.ToString() + ". " + value.Value.ToString());
                    XmlSerializer xml = new XmlSerializer(typeof(ObservableCollection<Plot>));

                    try
                    {
                        string s = Encoding.ASCII.GetString(ms.ToArray());
                        Plots = xml.Deserialize(ms) as ObservableCollection<Plot>;

                        foreach (Plot p in Plots)
                        {
                            p.Rebuild();
                            //Plots.Add(p);
                        }
                    }
                    catch (Exception e)
                    {
                        int i = 0;
                    }
                }

                if (nod.Contains("JPP_Site"))
                {

                    ObjectId plotId = nod.GetAt("JPP_Site");
                    Xrecord plotXRecord = (Xrecord)tr.GetObject(plotId, OpenMode.ForRead);
                    var buffers = plotXRecord.Data.AsArray();
                    GroundBearingPressure = (int)buffers[0].Value;
                    DefaultWidth = float.Parse((string)buffers[1].Value);
                } else
                {
                    GroundBearingPressure = 100;
                    DefaultWidth = 0.6f;
                }

                tr.Commit();
            }
        }

        private void Database_BeginSave(object sender, DatabaseIOEventArgs e)
        {
            Save();
        }

        private void AcDoc_BeginDocumentClose(object sender, DocumentBeginCloseEventArgs e)
        {
            Save();
        }

        public void Save()
        {
            //Load the data
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            Database acCurDb = acDoc.Database;
            try
            {
                using (DocumentLock dl = acDoc.LockDocument())
                {
                    using (Transaction tr = acCurDb.TransactionManager.StartTransaction())
                    {
                        // Find the NOD in the database
                        DBDictionary nod = (DBDictionary)tr.GetObject(acCurDb.NamedObjectsDictionaryId, OpenMode.ForWrite);

                        // We use Xrecord class to store data in Dictionaries
                        Xrecord plotXRecord = new Xrecord();

                        XmlSerializer xml = new XmlSerializer(typeof(ObservableCollection<Plot>));

                        //BinaryFormatter bf = new BinaryFormatter();
                        MemoryStream ms = new MemoryStream();
                        xml.Serialize(ms, Plots);
                        string s = Encoding.ASCII.GetString(ms.ToArray());

                        byte[] data = new byte[512];
                        int moreData = 1;
                        ResultBuffer rb = new ResultBuffer();
                        ms.Position = 0;
                        while (moreData > 0)
                        {
                            data = new byte[512];
                            moreData = ms.Read(data, 0, data.Length);
                            string dataString = Encoding.ASCII.GetString(data);
                            //dataString = dataString.Replace('\0', '\a');
                            TypedValue tv = new TypedValue((int)DxfCode.Text, dataString);
                            rb.Add(tv);
                        }

                        plotXRecord.Data = rb;

                        // Create the entry in the Named Object Dictionary
                        nod.SetAt("JPP_Plot", plotXRecord);
                        tr.AddNewlyCreatedDBObject(plotXRecord, true);

                        Xrecord siteXRecord = new Xrecord();
                        ResultBuffer siteRb = new ResultBuffer();

                        siteRb.Add(new TypedValue((int)DxfCode.Int32, GroundBearingPressure));
                        siteRb.Add(new TypedValue((int)DxfCode.Text, DefaultWidth));

                        siteXRecord.Data = siteRb;
                        nod.SetAt("JPP_Site", siteXRecord);
                        tr.AddNewlyCreatedDBObject(siteXRecord, true);

                        tr.Commit();
                    }
                }
            }
            catch (Exception e)
            {
                Application.ShowAlertDialog("Error saving - " + e.Message);
            }
        }

        public ObservableCollection<Plot> Plots { get; set; }

        public int GroundBearingPressure { get; set;}

        public float DefaultWidth { get; set; }
    }
}
