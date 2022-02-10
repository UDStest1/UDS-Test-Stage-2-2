using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Tooling.Connector;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Metadata;
using UDS_Test_Stage_2.Enums;
using CrmEarlyBound;

namespace UDS_Test_Stage_2
{
    class Program
    {
        static Random rand = new Random();

        static CrmServiceClient service;

        static List<Entity> CarClassList;
        static List<new_Transferlocation> TransferLocation;
        static List<Entity> CustomerList;

        static List<Entity> GetAllByQuery(QueryExpression query)
        {
            var result = new List<Entity>();
            EntityCollection resCol;

            if(query.PageInfo == null)
            {
                query.PageInfo = new PagingInfo
                {
                    Count = 5000,
                    PageNumber = 1,
                    PagingCookie = string.Empty
                };
            }

            do
            {
                resCol = service.RetrieveMultiple(query);
                if (resCol.Entities.Count > 0)
                {
                    result.AddRange(resCol.Entities.ToList());
                }
                if (resCol.MoreRecords)
                {
                    query.PageInfo.PageNumber += 1;
                    query.PageInfo.PagingCookie = resCol.PagingCookie;
                }
            } while (resCol.MoreRecords);

            return result;
        }

        static void SetupCarClassList()
        {
            //  ** Create  Car class list
            QueryExpression query = new QueryExpression()
            {
                Distinct = false,
                EntityName = "new_carclass",
                ColumnSet = new ColumnSet("new_carclassid", "new_classcode", "statecode", "new_price"),
                Criteria =
                {
                    Filters =
                    {
                        new FilterExpression
                        {
                            Conditions =
                            {
                                new ConditionExpression("statecode", ConditionOperator.Equal, 0),  // Only active state
                            },
                        }
                    }
                }
            };
            CarClassList = ((EntityCollection)service.RetrieveMultiple(query)).Entities.ToList();
        }

        static void SetupCustomerList()
        {
            // ** Create Customer List 
            QueryExpression query = new QueryExpression()
            {
                Distinct = false,
                EntityName = "contact",
                ColumnSet = new ColumnSet("contactid", "statecode"),
                Criteria =
                {
                    Filters =
                    {
                        new FilterExpression
                        {
                            Conditions =
                            {
                                new ConditionExpression("statecode", ConditionOperator.Equal, 0),  // Only active state
                            },
                        }
                    }
                }
            };
            CustomerList = GetAllByQuery(query);
        }
        static void SetupTransferLocationList()
        {
            // Create transfer location list
            TransferLocation = new List<new_Transferlocation>();
            TransferLocation.Add(new_Transferlocation.Airport);
            TransferLocation.Add(new_Transferlocation.Citycenter);
            TransferLocation.Add(new_Transferlocation.Office);
        }
        static void Setup()
        {
            SetupCarClassList();
            SetupCustomerList();
            SetupTransferLocationList();
        }

        static bool PrepareService()
        {
            string connectionString = @"
                AuthType=OAuth;
                Username=;
                Password=;
                Url=https://udstest1.crm4.dynamics.com/;
                AppId=51f81489-12ee-4a9e-aaae-a2591f45987d;
                RedirectUri=app://58145B91-0C36-4500-8554-080854F2AC97;
                LoginPrompt=Never;
                ";

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            service = new CrmServiceClient(connectionString);

            if (service.IsReady)
            {
                Console.WriteLine("CRM ready");
                return true;
            }
            else
            {
                Console.WriteLine(service.LastCrmError);
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
                return false;
            }
        }

        static EntityReference CreateReport(new_Transfertype tType, DateTime dat, Guid carId)
        {
            new_cartransferreport carTransferReport = new new_cartransferreport();

            carTransferReport.new_date = dat;
            carTransferReport.new_transfertype = tType;
            carTransferReport.new_carid = new EntityReference("new_car", carId);

            carTransferReport.new_description = (tType == new_Transfertype.Pickup ? "Pickup ": "Return")+" "+dat.ToShortDateString();

            int prob = rand.Next(1, 20);
            if (prob == 1) // 5%
            {
                carTransferReport.new_damages = true; // Yes
                carTransferReport.new_damagesdescription = "damage";
            }
            else
            {
                carTransferReport.new_damages = false; //No
            }

            Guid reportId = service.Create(carTransferReport);
            return new EntityReference("new_cartransferreport", reportId);
        }

        static Entity GetRandomCarClass()
        {
            return CarClassList[rand.Next(CarClassList.Count)];
        }
        static Entity GetRandomCarRespClass(string carClassId)
        {
            //  ** search cars with respect to car class
            QueryExpression query = new QueryExpression()
            {
                Distinct = false,
                EntityName = "new_car",
                ColumnSet = new ColumnSet("new_carclassid", "new_name", "statecode"),
                Criteria =
                {
                    Filters =
                    {
                        new FilterExpression
                        {
                            FilterOperator = LogicalOperator.And,
                            Conditions =
                            {
                                new ConditionExpression("new_carclassid", ConditionOperator.Equal, carClassId),
                                new ConditionExpression("statecode", ConditionOperator.Equal, 0)  // Only active state
                            },
                        },
                    }
                }
            };
            List<Entity> selectedCars = GetAllByQuery(query);
            return selectedCars[rand.Next(selectedCars.Count)];
        }

        static bool CheckCarRental(Guid carId, DateTime dateOfPickup, DateTime dateOfHandover)
        {
            using (svcContext context = new svcContext(service))
            {
                var recs = from rents in context.new_rentSet
                           where (rents.StatusCode.Value == new_rent_StatusCode.Canceled)
                           && (
                                (    // this car is fully leased at the moment
                                     (rents.new_reservedpickup <= dateOfPickup)
                                     && (rents.new_Reservedhandover >= dateOfHandover)
                                )
                                || ( // rental range partially overlaps
                                     (rents.new_reservedpickup >= dateOfPickup)
                                     && (rents.new_reservedpickup <= dateOfHandover)
                                     && (rents.new_Reservedhandover >= dateOfHandover)
                                 )
                                || ( // rental range partially overlaps
                                     (rents.new_reservedpickup <= dateOfPickup)
                                     && (rents.new_Reservedhandover >= dateOfPickup)
                                     && (rents.new_Reservedhandover <= dateOfHandover)
                              )
                            )
                            && (rents.new_carid.Id == carId)
                           select rents;

                if (recs.ToList().Count() != 0)
                {  // this car is currently rented, we are looking for another
                    return false;
                }
                return true;
            }
        }

        static EntityReference GetRandomCustomer()
        {
            return new EntityReference("contact", new Guid(CustomerList[rand.Next(CustomerList.Count)].Id.ToString()));
        }

        static DateTime GetRandomDay(DateTime startDate, int Duration)
        {
            return startDate.AddDays(rand.Next(Duration+1)); 
        }
                
        static new_Transferlocation GetRandomTransferLocation()
        {
            return TransferLocation[rand.Next(TransferLocation.Count)]; 
        }
        
        static bool GetPaidStatus(new_rent_StatusCode status)
        {
            int prob = rand.Next(1, 10000);
            bool result = false;     // false - No  true - Yes  // another status reason - No
            if (status == new_rent_StatusCode.Confirmed)
            {
                if (prob <= 9000)
                {
                    result = true;
                }
            }
            else if (status == new_rent_StatusCode.Renting)
            {
                if (prob <= 9990)
                {
                    result = true;
                }
            }
            else if (status == new_rent_StatusCode.Returned)
            {
                if (prob <= 9998)
                {
                    result = true;                    
                }
            }
            return result;
        }

        static new_rentState GetStatusState(new_rent_StatusCode statuscode)
        {
            new_rentState result = new new_rentState();
            if ((statuscode == new_rent_StatusCode.Returned) || (statuscode == new_rent_StatusCode.Canceled))
            {
                result = new_rentState.Inactive;
            }
            else
            {
                result = new_rentState.Active;
            }
            return result;
        }

        static new_rent_StatusCode GetRandomStatusReason()
        {
            int prob = rand.Next(1,20);
            new_rent_StatusCode StatusCode;
            if (prob==1) // 5% - Created  
            {
                StatusCode = new_rent_StatusCode.Created;
            }
            else if (prob == 2) // 5% - Confirmed
            {
                StatusCode = new_rent_StatusCode.Confirmed;
            }
            else if (prob == 3) // 5% - Renting
            {
                StatusCode = new_rent_StatusCode.Renting;
            }
            else if ((prob >= 4) && (prob <= 18)) // 75% - returned
            {
                StatusCode = new_rent_StatusCode.Returned;
            }
            else // 10% - Canceled
            {
                StatusCode = new_rent_StatusCode.Canceled;
            }

            return StatusCode;
        }

        static void Main(string[] args)
        {

            if(!PrepareService())
            {
                return;
            }

            Setup();

            int TotalSamples = 2; // 40000;

            DateTime BaseStartDate = new DateTime(2019, 1, 1);
            DateTime BaseEndDate = new DateTime(2020, 12, 31);
            int MaxDuration = (BaseEndDate - BaseStartDate).Days;

            for (int CurrentSample = 1; CurrentSample <= TotalSamples; CurrentSample++) // loop to create sample data
            {
                new_rent rent = new new_rent();

                string nameOfSample = "        " + CurrentSample.ToString();
                nameOfSample = nameOfSample.Substring(nameOfSample.Length - 7, 7);
                rent.new_name = "3 Sample  -" + nameOfSample;

                bool GoodSampleData = false;
                while (!GoodSampleData) 
                {
                    new_rent_StatusCode rentStatusCode = GetRandomStatusReason();
                    rent.StatusCode = rentStatusCode;
                    rent.StateCode = GetStatusState(rentStatusCode);

                    int Duration = rand.Next(1, 30); // Duration of rent 1-30 days
                    
                    DateTime rentDatePickup = GetRandomDay(BaseStartDate, MaxDuration - Duration);
                    rent.new_reservedpickup = rentDatePickup;
                    DateTime rentDateHandover = rentDatePickup.AddDays(Duration);
                    rent.new_Reservedhandover = rentDateHandover;

                    if ((rentStatusCode == new_rent_StatusCode.Renting) || (rentStatusCode == new_rent_StatusCode.Returned))
                    {
                        rent.new_actualpickup = rentDatePickup;
                    }
                    if (rentStatusCode == new_rent_StatusCode.Returned)
                    {
                        rent.new_actualreturn = rentDateHandover;
                    }

                    Entity randomCarClass = GetRandomCarClass();
                    rent.new_carclassid = new EntityReference("new_carclass", randomCarClass.Id);

                    Entity selectedCar = GetRandomCarRespClass(rent.new_carclassid.Id.ToString());
                    if (!CheckCarRental(selectedCar.Id, rentDatePickup, rentDateHandover))
                    {  // this car is currently rented, we are looking for another
                        continue;
                    }
                    rent.new_carid = new EntityReference("new_car", selectedCar.Id);
                    
                    rent.new_pickuplocation = GetRandomTransferLocation();
                    rent.new_returnlocation = GetRandomTransferLocation();
                    
                    rent.new_price = new Money(randomCarClass.GetAttributeValue<Money>("new_price").Value * Duration); // Approx

                    rent.new_customer = GetRandomCustomer();

                    rent.new_paid = GetPaidStatus(rentStatusCode);

                    if ((rentStatusCode == new_rent_StatusCode.Renting) || (rentStatusCode == new_rent_StatusCode.Returned))
                    {
                        rent.new_pickupreportid = CreateReport(new_Transfertype.Pickup, rentDatePickup, selectedCar.Id);
                    }

                    if (rentStatusCode == new_rent_StatusCode.Returned)
                    {
                        rent.new_returnreport = CreateReport(new_Transfertype.Return, rentDateHandover, selectedCar.Id);
                    }

                    GoodSampleData = true;
                }
                Guid rentId = service.Create(rent);
                Console.WriteLine(CurrentSample.ToString() + " - " + rentId.ToString());
            }

            Console.WriteLine("Done. Press any key to exit");
            Console.ReadKey();
        }
    }
}
