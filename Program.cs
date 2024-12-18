using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static TelMVPositions.Program;

namespace TelMVPositions
{
    class Program
    {
        static void Main(string[] args)
        {
            Stopwatch stopWatch = new Stopwatch();
            TimeSpan readExecutionTime = new();
            TimeSpan closestExecutionTime = new();
            string path = Path.Combine(AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory, @"VehiclePositions.dat");

            List<(float Latitude, float Longitude)> targetCoordinates = new List<(float, float)>
            {
                (34.544909f, -102.100843f),
                (32.345544f, -99.123124f),
                (33.234235f, -100.214124f), 
                (35.195739f, -95.348899f),
                (31.895839f, -97.789573f),
                (32.895839f, -101.789573f), 
                (34.115839f, -100.225732f), 
                (32.335839f, -99.992232f), 
                (33.535339f, -94.792232f),
                (32.234235f, -100.222222f), 
            };

            try
            {
                // Read the vehicle data from the file into a Dictionary of Vehicle objects
                stopWatch.Start();

                Dictionary<string, Vehicle> vehicles = ReadFile(path);

                stopWatch.Stop();
                readExecutionTime = stopWatch.Elapsed;
                stopWatch.Reset();
                Console.WriteLine("Data file read execution time : " + readExecutionTime.TotalMilliseconds + " ms");
                

                // Use Parallel.ForEach to process each target coordinate in parallel
                stopWatch.Start();
                Parallel.ForEach(targetCoordinates, target =>
                {
                    var closestVehicle = FindClosestVehicle(vehicles, target.Latitude, target.Longitude);
                    if (closestVehicle != null)
                    {
                        Console.WriteLine($"The nearest vehicle to ({target.Latitude}, {target.Longitude}) is {closestVehicle.VehicleRegistration}");
                    }
                });
                stopWatch.Stop();
                closestExecutionTime = stopWatch.Elapsed;
                Console.WriteLine("Closest execution time : " + closestExecutionTime.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error : " + ex.Message);
            }
            finally
            {
                var totTime = readExecutionTime.TotalMilliseconds + closestExecutionTime.TotalMilliseconds;
                Console.WriteLine("Total execution time : " + totTime);
            }
        }

        // Function to read the .dat file into a Dictionary of Vehicle objects
        static Dictionary<string, Vehicle> ReadFile (string filePath)
        {
            Dictionary<string, Vehicle> vehicles = new Dictionary<string, Vehicle>();

            // Use a memory stream to load the entire file into memory (if the file is not extremely large)
            byte[] fileData = File.ReadAllBytes(filePath);

            // Define the fixed record size (10 bytes for registration, 8 bytes for latitude, 8 bytes for longitude)
            int regNumberLength = 10;
            int recordSize = regNumberLength + 4 + 4 + 8;

            // The number of records in the file (calculated by dividing total bytes by record size)
            int recordCount = fileData.Length / recordSize;

            // Read the records from the file data in one pass
            for (int i = 0; i < recordCount; i++)
            {
                // Calculate the starting index for this record
                int recordStartIndex = i * recordSize;

                // Extract the registration number (10 bytes)
                string regNumber = Encoding.ASCII.GetString(fileData, recordStartIndex, regNumberLength).Trim();

                // Extract latitude and longitude (4 bytes each)
                float latitude = BitConverter.ToSingle(fileData, recordStartIndex + regNumberLength);
                float longitude = BitConverter.ToSingle(fileData, recordStartIndex + regNumberLength + 4);

                ulong recordedTimeUTC = BitConverter.ToUInt64(fileData, recordStartIndex + regNumberLength + 4 + 4);

                // Add the vehicle record to the dictionary
                if(!string.IsNullOrEmpty(regNumber))
                {
                    vehicles[regNumber] = new Vehicle
                    {
                        VehicleRegistration = regNumber,
                        Latitude = latitude,
                        Longitude = longitude,
                        RecordedTimeUTC = recordedTimeUTC
                    };
                }
            }

            return vehicles;
        }

        // Function to calculate the Haversine distance between two lat/long points
        static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0; // Earth's radius in kilometers

            double lat1Rad = ToRadians(lat1);
            double lon1Rad = ToRadians(lon1);
            double lat2Rad = ToRadians(lat2);
            double lon2Rad = ToRadians(lon2);

            double dLat = lat2Rad - lat1Rad;
            double dLon = lon2Rad - lon1Rad;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c; // Distance in kilometers
        }

        // Helper function to convert degrees to radians
        static double ToRadians(double degree) => degree * (Math.PI / 180.0);

        static Vehicle FindClosestVehicle(Dictionary<string, Vehicle> vehicles, double targetLat, double targetLon)
        {
            Vehicle? closestVehicle = null;

            // Use Parallel LINQ to find the closest vehicle faster
            closestVehicle = vehicles.Values.AsParallel()
                .OrderBy(vehicle => Haversine(targetLat, targetLon, vehicle.Latitude, vehicle.Longitude))
                .FirstOrDefault();

            return closestVehicle;
        }

        public class Vehicle
        {
            public int? VehicleId { get; set; }
            public string? VehicleRegistration { get; set; }
            public float Latitude { get; set; }
            public float Longitude { get; set; }
            public ulong RecordedTimeUTC { get; set; }
        }
    }
}
