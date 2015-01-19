﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Globalization;
using MathNet.Numerics.LinearAlgebra.Double;
using Models.Core;

namespace Models.Soils
{

    /// <summary>
    /// 
    /// </summary>
    public class ZoneWaterAndN
    {
        /// <summary>The zone</summary>
        public string Name;
        /// <summary>The amount</summary>
        public double[] Water;
        public double[] NO3N;

    }
    public class CropWaterUptakes
    {
        /// <summary>Crop</summary>
        public ICrop Crop;
        /// <summary>List of uptakes</summary>
        public List<ZoneWaterAndN> Zones;

        public CropWaterUptakes()
        {
            Zones = new List<ZoneWaterAndN>();
        }
    }

    /// <summary>
    /// A soil arbitrator model
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.GridView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    public class SoilArbitrator : Model
    {
        /// <summary>The simulation</summary>
        [Link]
        Simulation Simulation = null;

        /// <summary>
        /// The following event handler will be called once at the beginning of the simulation
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("Commencing")]
        private void OnSimulationCommencing(object sender, EventArgs e) {}

        /// <summary>Called when [do soil arbitration].</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        /// <exception cref="ApsimXException">Calculating Euler integration. Number of UptakeSums different to expected value of iterations.</exception>
        [EventSubscribe("DoWaterArbitration")]
        private void OnDoSoilArbitration(object sender, EventArgs e)
        {

            // Start by recording the water content in all zones
            List<ZoneWaterAndN> SoilState1 = new List<ZoneWaterAndN>();
            foreach (Zone Z in Apsim.Children(Simulation, typeof(Zone)))
            {
                ZoneWaterAndN NewZ = new ZoneWaterAndN();
                NewZ.Name = Z.Name;
                Soil soil = Apsim.Child(Z, typeof(Soil)) as Soil;
                NewZ.Water = soil.Water;
                NewZ.NO3N = soil.NO3N;
                SoilState1.Add(NewZ);
            }

            // Modified Euler Method
            //List<CropWaterUptakes> Estimate1 = new List<CropWaterUptakes>();
            //Estimate1 = GetCropWaterUptakes(SoilState1);
            //
            //List<ZoneWaterAndN> SoilState2 = new List<ZoneWaterAndN>();
            //SoilState2 = RemoveUptakeFromSW(Estimate1, SoilState1, 1.0);
            //
            //List<CropWaterUptakes> Estimate2 = new List<CropWaterUptakes>();
            //Estimate2 = GetCropWaterUptakes(SoilState2);

            //Runge Kutta
            List<CropWaterUptakes> Estimate1 = new List<CropWaterUptakes>();
            Estimate1 = GetCropWaterUptakes(SoilState1);

            List<CropWaterUptakes> Estimate2 = new List<CropWaterUptakes>();
            Estimate2 = GetCropWaterUptakes(RemoveUptakeFromSW(Estimate1, SoilState1, 0.5));

            List<CropWaterUptakes> Estimate3 = new List<CropWaterUptakes>();
            Estimate3 = GetCropWaterUptakes(RemoveUptakeFromSW(Estimate2, SoilState1, 0.5));

            List<CropWaterUptakes> Estimate4 = new List<CropWaterUptakes>();
            Estimate4 = GetCropWaterUptakes(RemoveUptakeFromSW(Estimate3, SoilState1, 1.0));


            List<CropWaterUptakes> UptakesFinal = new List<CropWaterUptakes>();
            foreach (CropWaterUptakes U in Estimate1)
            {
                CropWaterUptakes CWU = new CropWaterUptakes();
                CWU.Crop=U.Crop;
                foreach (ZoneWaterAndN ZW1 in U.Zones)
                {
                    ZoneWaterAndN NewZ = new ZoneWaterAndN();
                    NewZ.Name = ZW1.Name;
                    // Modifield Euler
                    //Z.Water = Utility.Math.Add(Utility.Math.Multiply_Value(GetUptake(CWU.Crop.CropType, Z.Name, Estimate1),0.5),
                    //                           Utility.Math.Multiply_Value(GetUptake(CWU.Crop.CropType, Z.Name, Estimate2),0.5));
                    
                    //Runge Kutta
                    NewZ.Water = Utility.Math.Add(Utility.Math.Multiply_Value(GetUptake(CWU.Crop.CropType, NewZ.Name, Estimate1), 0.166666),
                                               Utility.Math.Multiply_Value(GetUptake(CWU.Crop.CropType, NewZ.Name, Estimate2), 0.333333));
                    NewZ.Water = Utility.Math.Add(NewZ.Water,Utility.Math.Multiply_Value(GetUptake(CWU.Crop.CropType, NewZ.Name, Estimate3), 0.333333));
                    NewZ.Water = Utility.Math.Add(NewZ.Water, Utility.Math.Multiply_Value(GetUptake(CWU.Crop.CropType, NewZ.Name, Estimate4), 0.16666));
                    
                    CWU.Zones.Add(NewZ);
                }
                
                UptakesFinal.Add(CWU);

            }

            foreach (CropWaterUptakes Uptake in UptakesFinal)
            {
                Uptake.Crop.SetSWUptake(Uptake.Zones);
                //CropUptakeInfo CropUptake = Uptakes.Find(u => u.Crop == crop);
            }


        }
        private List<CropWaterUptakes> GetCropWaterUptakes(List<ZoneWaterAndN> SWs)
        {
            List<CropWaterUptakes> Uptakes = new List<CropWaterUptakes>();
            foreach (ICrop crop in Apsim.ChildrenRecursively(Simulation, typeof(ICrop)))
            {
                if (crop.IsAlive)
                {
                    CropWaterUptakes Uptake = new CropWaterUptakes();
                    Uptake.Crop = crop;
                    Uptake.Zones = crop.GetSWUptakes(SWs);
                    Uptakes.Add(Uptake);
                }
            }
            return Uptakes;
        }
        private List<ZoneWaterAndN> RemoveUptakeFromSW(List<CropWaterUptakes> Uptakes, List<ZoneWaterAndN> SW, double fraction)
        {
            List<ZoneWaterAndN> NewSW = new List<ZoneWaterAndN>();
            foreach (ZoneWaterAndN Z in SW)
                NewSW.Add(Z);

            foreach (CropWaterUptakes C in Uptakes)
                foreach (ZoneWaterAndN Z in C.Zones)
                    foreach (ZoneWaterAndN NewZ in NewSW)
                        if (Z.Name == NewZ.Name)
                            NewZ.Water = Utility.Math.Subtract(NewZ.Water, Utility.Math.Multiply_Value(Z.Water,fraction));

            return NewSW;
        }
        private double[] GetUptake(string CropType, string ZoneName, List<CropWaterUptakes> Uptakes)
        {
            bool found = false;
            double[] SW = null;
            foreach(CropWaterUptakes U in Uptakes)
                if (U.Crop.CropType==CropType)
                    foreach(ZoneWaterAndN Z in U.Zones)
                        if (Z.Name == ZoneName)
                        {
                            if (found) 
                            { }
                            found = true;
                            SW = new double[Z.Water.Length];
                            SW = Z.Water;
                        }
            if (!found)
                throw (new Exception("Cannot find uptake for"+CropType+" "+ZoneName));

            
            return SW;
        }

    }
}