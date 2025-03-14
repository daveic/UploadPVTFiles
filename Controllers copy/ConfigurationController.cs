using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using BDA_Pricer.Models;
using Newtonsoft.Json;
using System.Security.Claims;

namespace BDA_Pricer.Controllers
{
    public partial class BDAPricerController : Controller
    {
        private readonly string _jsonPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot/data/db-tables.json");
        private const string AllowedUserId = "ruh7amOOPc4KJ93jfboUFUWzgH67CsC1uusoQ9p5dL0";

        [Route("BDAPricer/Configuration")]
        [AuthorizeForScopes(ScopeKeySection = "DownstreamApi:Scopes")]
        public ActionResult Configuration()
        {            
            ViewBag.UserID = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            ViewBag.UserName = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
            // Verifica se l'utente ha il permesso di visualizzare questa view
            if (ViewBag.userId != AllowedUserId)
            {
                // Puoi redirigere ad una pagina di accesso negato o alla home
                return RedirectToAction("AccessDenied", "Index");
            }
            TabelleJson db_tables = GetFromDB(); 
            ViewBag.LastTableMod = TimeMod.UpdateTimeDB("Table", "GET").ToString("dd/MM/yyyy HH:mm");
            ViewBag.LastDBMod = TimeMod.UpdateTimeDB("DB", "GET").ToString("dd/MM/yyyy HH:mm");
            return View(db_tables);
        }

        public TabelleJson GetFromDB()
        {
            string filePath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot/data/db-tables.json");
            string json = System.IO.File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new Exception("Il file JSON è vuoto o nullo.");
            }
            else
            {
                TabelleJson? db_tables = JsonConvert.DeserializeObject<TabelleJson>(json);

                if (db_tables == null)
                {
                    throw new Exception("Errore nella deserializzazione del file JSON. Il file potrebbe essere vuoto o malformato.");
                }
                else return db_tables;
            }
        }

        [Route("BDAPricer/DownloadJson")]
        [AuthorizeForScopes(ScopeKeySection = "DownstreamApi:Scopes")]
        public IActionResult DownloadJson()
        {
            string filePath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot/data/db-tables.json");

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Il file JSON non esiste.");
            }

            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            string fileName = "db-tables.json";

            return File(fileBytes, "application/json", fileName);
        }      
        
        public ActionResult Configuration_Details(int id)
        {
            TabelleJson db_tables = GetFromDB();
            DN_Sched_Tube? dN_Sched_Tube = db_tables.DN_Sched_Tube.FirstOrDefault(x => x.ID == id);
            return PartialView(dN_Sched_Tube);
        }

        [AuthorizeForScopes(ScopeKeySection = "DownstreamApi:Scopes")]
        public ActionResult Configuration_Delete(int id)
        {
            return Configuration_Details(id);
        }

        [HttpPost]
        [AuthorizeForScopes(ScopeKeySection = "DownstreamApi:Scopes")]
        public ActionResult Configuration_Delete(DN_Sched_Tube dN_Sched_Tube)
        {
            string filePath = "wwwroot/data/db-tables.json"; // Percorso del file JSON
            TabelleJson db_tables = GetFromDB();
            db_tables.DN_Sched_Tube.RemoveAll(e => e.ID == dN_Sched_Tube.ID);
            

            // Serializza di nuovo in JSON
            string updatedJson = JsonConvert.SerializeObject(db_tables, Formatting.Indented);
            System.IO.File.WriteAllText(filePath, updatedJson); // Usa System.IO.File per evitare conflitti
            if(TimeMod.UpdateTimeDB("Table", "UPDATE") == DateTime.Now){
                _notyf.Warning("Codice rimosso correttamente.");
            }            

            return RedirectToAction(nameof(Configuration));
        }

        [AuthorizeForScopes(ScopeKeySection = "DownstreamApi:Scopes")]
        public ActionResult Configuration_Edit(int id)
        {
            return Configuration_Details(id);
        }
        [HttpPost]
        [AuthorizeForScopes(ScopeKeySection = "DownstreamApi:Scopes")]
        public ActionResult Configuration_Edit(DN_Sched_Tube selectedCode)
        {
            string filePath = "wwwroot/data/db-tables.json"; // Percorso del file JSON
            TabelleJson db_tables = GetFromDB();
            // Trova l'entry che vuoi modificare nella tabella DN_Sched_Tube
            var entryToEdit = db_tables.DN_Sched_Tube.FirstOrDefault(x => x.ID == selectedCode.ID);
            
            // Se l'entry esiste, aggiorna i suoi valori
            if (entryToEdit != null)
            {
                entryToEdit.Meter_Price = selectedCode.Meter_Price;
                // Riscrivi il file JSON con le modifiche
                string updatedJson = JsonConvert.SerializeObject(db_tables, Formatting.Indented);
                System.IO.File.WriteAllText(filePath, updatedJson);
                if (TimeMod.UpdateTimeDB("Table", "UPDATE") == DateTime.Now)
                {
                    _notyf.Success("Codice modificato correttamente.");
                }
            }
            else
            {
                Console.WriteLine("Entry non trovata.");
            }            
            return RedirectToAction(nameof(Configuration));
        }        
    }
}
