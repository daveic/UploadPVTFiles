using System.Diagnostics;
using BDA_Pricer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using AspNetCoreHero.ToastNotification.Abstractions;
using System.Security.Claims;

namespace BDA_Pricer.Controllers
{
    [Authorize]
    public partial class BDAPricerController(ILogger<BDAPricerController> logger, GraphServiceClient graphServiceClient, INotyfService notyf) : Controller
    {
        private readonly GraphServiceClient _graphServiceClient = graphServiceClient;
        private readonly ILogger<BDAPricerController> _logger = logger;
        private readonly INotyfService _notyf = notyf;

        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        public IActionResult Index()
        {
            // Recupera l'ID Token (OIDC Token)
            ViewBag.UserID = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            ViewBag.UserName = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
            BDACode item = new()
                {
                    TakeNumber = 0,
                    TakePos = '-',
                    Lung = 0,
                    TakeDist = '-',
                    DN_Sched_Tube = "-",
                    Connection = "-",
                    ConnectionDrain = "-",
                    Materiale = "-",
                    DrainPos = '-'
                };
            return View(item);
        }

        [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
        [HttpPost]
       // [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index(BDACode bda_code)
        {
            // Recupera l'ID Token (OIDC Token)
            ViewBag.UserID = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            ViewBag.UserName = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
            if (ModelState.IsValid)
            {
                bda_code.Input_Code = bda_code.Input_Code?.ToUpper();
                if (bda_code.Input_Code?.StartsWith("BDA") == true && bda_code.Input_Code.Length >= 16 && bda_code.Input_Code.Length <= 20)
                {
                    ProcessBDACode(bda_code, ModelState);
                    if (ModelState.ErrorCount > 0)
                    {
                        ViewBag.Error = ModelState.ElementAt(0).Value?.Errors?.FirstOrDefault()?.ErrorMessage;
                        return await Task.FromResult<IActionResult>(View(bda_code));
                    }
                }
                else
                {
                    ViewBag.Error = "Il codice deve iniziare con 'BDA' e avere tra 16 e 20 caratteri.";
                    return await Task.FromResult<IActionResult>(View(bda_code));
                }

                string filePath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot/data/db-tables.json");
                string json = System.IO.File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new Exception("Il file JSON è vuoto o nullo.");
                } else
                {
                    TabelleJson? db_tables = JsonConvert.DeserializeObject<TabelleJson>(json) ?? throw new Exception("Errore nella deserializzazione del file JSON. Il file potrebbe essere vuoto o malformato.");

                    string MaterialAggregate = string.Concat(bda_code.Material, bda_code.PipeDim, bda_code.PipeSched).ToLower(); //Preparo stringa MaterialAggregate di 3 caratteri
                    IEnumerable<DN_Sched_Tube> DN_Sched_Tube = db_tables.DN_Sched_Tube.Where(x => x.MaterialsCode == MaterialAggregate); //Recupero da db riga associata a MaterialAggregate
                    bda_code.DN_Sched_Tube = DN_Sched_Tube.FirstOrDefault()?.DN_Sched_Tube_Code; //Estraggo codice esteso da db

                    IEnumerable<Materials> Material = db_tables.Materials.Where(x => x.ID == bda_code.Material); //Recupero da db riga associata a MaterialAggregate
                    bda_code.Materiale = Material.FirstOrDefault()?.Materiale; //Estraggo codice esteso da db

                    string ConnectionCode = string.Concat(bda_code.TakeDim, bda_code.TakeType, bda_code.Material); //Preparo stringa Connection di 3 caratteri
                    IEnumerable<Connections_Details> ConnectionInfo = db_tables.Connections.Where(x => x.ConnCode == ConnectionCode); //Recupero da db riga associata a ConnectionCode
                    bda_code.Connection = ConnectionInfo.FirstOrDefault()?.Connection; //Estraggo codice esteso da db

                    string ConnectionCodeDrain = string.Concat(bda_code.DrainDim, bda_code.DrainType, bda_code.Material); //Preparo stringa ConnectionDrain di 3 caratteri
                    IEnumerable<Connections_Details> ConnectionInfoDrain = db_tables.Connections.Where(x => x.ConnCode == ConnectionCodeDrain); //Recupero da db riga associata a ConnectionCode
                    bda_code.ConnectionDrain = ConnectionInfoDrain.FirstOrDefault()?.Connection; //Estraggo codice esteso da db

                    double costoTotale = CostCalc(bda_code, (char)bda_code.TakePos, db_tables, DN_Sched_Tube, ConnectionInfo, ConnectionInfoDrain);
                    bda_code.CostoTotRicarico = Math.Round((costoTotale * 1.625), 2);
                }
                return await Task.FromResult<IActionResult>(View(bda_code));
            }
            ViewBag.Error = "Codice non elaborabile.";
            return await Task.FromResult<IActionResult>(View(bda_code));
        }

        public static string ExtractFirstElement(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            if (char.IsLetter(input[0]) || !char.IsDigit(input[0]))
                return input[0].ToString();

            StringBuilder numberSequence = new();

            foreach (char c in input)
            {
                if (char.IsDigit(c))
                    numberSequence.Append(c);
                else
                    break;
            }

            return numberSequence.ToString();
        }
        public static string RemovePrefix(string input, string prefix)
        {
            if (!string.IsNullOrEmpty(input) && input.StartsWith(prefix))
            {
                return input[prefix.Length..];
            }
            return input;
        }
        public void ProcessBDACode(BDACode bda_code, ModelStateDictionary ModelState)
        {
            if (bda_code == null || string.IsNullOrEmpty(bda_code.Input_Code) || !bda_code.Input_Code.StartsWith("BDA"))
            {
                ModelState.AddModelError("Input_Code", "Il codice inserito è errato o mancante.");
                return;
            }
            try
            {
                string substring = bda_code.Input_Code[3..]; // Rimuove "BDA"

                // Estrai TakeNumber
                string extractedString = ExtractFirstElement(substring);
                if (int.TryParse(extractedString, out int extractedNumber))
                {
                    if(extractedNumber >=5 && extractedNumber <= 24) bda_code.TakeNumber = extractedNumber;
                    else { ModelState.AddModelError("Input_Code", $"Errore nel codice: TakeNumber può assumere valore compreso tra 5 e 24. Immesso valore {extractedNumber} fuori range."); return; }
                }
                else
                {
                    ModelState.AddModelError("Input_Code", $"Errore nel codice: TakeNumber mancante. Atteso valore numerico. Inserito '{extractedString}'");
                    return;
                }
                substring = substring[extractedString.Length..];

                // Estrai TakePos
                bda_code.TakePos = ExtractFirstElement(substring)[0];
                if (bda_code.TakePos != 'L' && bda_code.TakePos != 'S') { ModelState.AddModelError("Input_Code", $"Errore nel codice: TakePos può assumere valore 'L' o 'S'. Immesso valore '{bda_code.TakePos}' fuori range."); return; }
                substring = substring[1..];

                // Estrai Material
                bda_code.Material = ExtractFirstElement(substring).FirstOrDefault();
                char[] materialCodes = ['S', 'M', 'B', 'P', 'F', 'D', 'E', 'U', 'V', 'Q'];
                if (!materialCodes.Contains(bda_code.Material.GetValueOrDefault())) { ModelState.AddModelError("Input_Code", $"Errore nel codice: Material può assumere valore 'S', 'M', 'B', 'P', 'F', 'D', 'E', 'U', 'V' o 'Q'. Immesso valore '{bda_code.Material}' fuori range."); return; }
                substring = substring[1..];

                // Estrai PipeDim
                bda_code.PipeDim = ExtractFirstElement(substring).FirstOrDefault();
                char[] pipeDims = ['B', 'C', 'D'];
                if (!pipeDims.Contains(bda_code.PipeDim.GetValueOrDefault())) { ModelState.AddModelError("Input_Code", $"Errore nel codice: PipeDim può assumere valore 'B', 'C' o 'D'. Immesso valore '{bda_code.PipeDim}' fuori range."); return; }
                substring = substring[1..];

                // Estrai PipeSched
                bda_code.PipeSched = ExtractFirstElement(substring).FirstOrDefault();
                char[] scheds = ['A', 'B', 'C', 'D'];
                if (!scheds.Contains(bda_code.PipeSched.GetValueOrDefault())) { ModelState.AddModelError("Input_Code", $"Errore nel codice: PipeSched può assumere valore 'A', 'B', 'C' o 'D'. Immesso valore '{bda_code.PipeSched}' fuori range."); return; }
                substring = substring[1..];
                
                // Estrai InDim                
                extractedString = ExtractFirstElement(substring);
                int[] dims = [2, 4, 6, 8, 12, 16];
                if (int.TryParse(extractedString, out extractedNumber))
                {
                    if (!dims.Contains(extractedNumber)) { ModelState.AddModelError("Input_Code", $"Errore nel codice: InDim può assumere valore 2, 4, 6, 8, 12 o 16. Immesso valore '{extractedNumber}' fuori range."); return; }
                    bda_code.InDim = extractedNumber;
                }
                else
                {
                    ModelState.AddModelError("Input_Code", $"Errore nel codice: InDim mancante. Atteso valore numerico. Inserito '{extractedString}'");
                    return;
                }
                substring = substring[extractedString.Length..];

                // Estrai InType
                bda_code.InType = ExtractFirstElement(substring).FirstOrDefault();
                char[] types = ['N', 'K', 'R', 'E', 'F', 'P', 'Z'];
                if (!types.Contains(bda_code.InType.GetValueOrDefault())) { ModelState.AddModelError("Input_Code", $"Errore nel codice: InType può assumere valore 'N', 'K', 'R', 'E', 'F', 'P' o 'Z'. Immesso valore '{bda_code.InType}' fuori range."); return; }
                substring = substring[1..];

                // Estrai TakeDim
                extractedString = ExtractFirstElement(substring);
                if (int.TryParse(extractedString, out extractedNumber))
                {
                    bda_code.TakeDim = extractedNumber;
                    if (!dims.Contains(extractedNumber)) { ModelState.AddModelError("Input_Code", $"Errore nel codice: TakeDim può assumere valore 2, 4, 6, 8, 12 o 16. Immesso valore '{extractedNumber}' fuori range."); return; }
                }
                else
                {
                    ModelState.AddModelError("Input_Code", $"Errore nel codice: TakeDim mancante. Atteso valore numerico. Inserito '{extractedString}'");
                    return;
                }
                substring = substring[extractedString.Length..];

                // Estrai TakeType
                bda_code.TakeType = ExtractFirstElement(substring).FirstOrDefault();
                if (!types.Contains(bda_code.TakeType.GetValueOrDefault())) { ModelState.AddModelError("Input_Code", $"Errore nel codice: TakeType può assumere valore 'N', 'K', 'R', 'E', 'F', 'P' o 'Z'. Immesso valore '{bda_code.TakeType}' fuori range."); return; }
                substring = substring[1..];

                // Estrai TakeDist
                bda_code.TakeDist = ExtractFirstElement(substring).FirstOrDefault();
                if (bda_code.TakeDist != 'A' && bda_code.TakeDist != 'B') { ModelState.AddModelError("Input_Code", $"Errore nel codice: TakeDist può assumere valore 'A' o 'B'. Immesso valore '{bda_code.TakeDist}' fuori range."); return; }
                substring = substring[1..];

                // Estrai DrainDim
                extractedString = ExtractFirstElement(substring);
                if (int.TryParse(extractedString, out extractedNumber))
                {
                    if (!dims.Contains(extractedNumber)) { ModelState.AddModelError("Input_Code", $"Errore nel codice: DrainDim può assumere valore 2, 4, 6, 8, 12 o 16. Immesso valore '{extractedNumber}' fuori range."); return; }
                    bda_code.DrainDim = extractedNumber;
                }
                else
                {
                    ModelState.AddModelError("Input_Code", $"Errore nel codice: DrainDim mancante. Atteso valore numerico. Inserito '{extractedString}'");
                    return;
                }
                substring = substring[extractedString.Length..];

                // Estrai DrainType
                bda_code.DrainType = ExtractFirstElement(substring).FirstOrDefault();
                if (!types.Contains(bda_code.DrainType.GetValueOrDefault())) { ModelState.AddModelError("Input_Code", $"Errore nel codice: DrainType può assumere valore 'N', 'K', 'R', 'E', 'F', 'P' o 'Z'. Immesso valore '{bda_code.DrainType}' fuori range."); return; }
                substring = substring[1..];

                // Estrai DrainPos
                bda_code.DrainPos = ExtractFirstElement(substring).FirstOrDefault();
                if (bda_code.DrainPos != 'L' && bda_code.DrainPos != 'B') { ModelState.AddModelError("Input_Code", $"Errore nel codice: DrainPos può assumere valore 'L' o 'B'. Immesso valore '{bda_code.DrainPos}' fuori range."); return; }
                substring = substring[1..];

                if (substring != "") { ModelState.AddModelError("Input_Code", "Errore nel codice: caratteri residui non validi."); return; }

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Input_Code", $"Errore durante l'elaborazione del codice: {ex.Message}");
            }
            return;
        }
        public double CostCalc(BDACode bda_code, char StacType, TabelleJson db_tables, IEnumerable<DN_Sched_Tube> DN_Sched_Tube, IEnumerable<Connections_Details> ConnectionInfo, IEnumerable<Connections_Details> ConnectionInfoDrain)
        {
            double costoFresatura = 0;
            double costoDrain = 0;
            double costoSaldaturaDrain = 0;
            if (StacType == 'L')
            {
                int drain = 0;
                //Calcolo Lunghezza
                if (bda_code.TakeDist == 'A') bda_code.Lung = (bda_code.TakeNumber * 70) - 70 + 50;
                else if (bda_code.TakeDist == 'B') bda_code.Lung = (bda_code.TakeNumber * 100) - 100 + 50;

                //Costo Drain
                if (bda_code.DrainPos == 'L') drain = 1;
                else if (bda_code.DrainPos == 'B') drain = 0;
                costoDrain = (double)((ConnectionInfoDrain.FirstOrDefault()?.Price ?? 0) * drain);
                //Costo Saldatura Drain
                costoSaldaturaDrain = (double)((ConnectionInfoDrain.FirstOrDefault()?.Sald_Stacchi_Price ?? 0) * drain);
                //Costo Fresatura
                costoFresatura = (double)((ConnectionInfo.FirstOrDefault()?.Fresa_Price ?? 0) * (bda_code.TakeNumber + drain));
            }
            else if (StacType == 'S'){
                //Calcolo Lunghezza
                if (bda_code.TakeDist == 'A') bda_code.Lung = (bda_code.TakeNumber * 70 / 2) - 70 + 50;
                else if (bda_code.TakeDist == 'B') bda_code.Lung = (bda_code.TakeNumber * 100 / 2) - 100 + 50;

                //Costo Fresatura! manca +drain!!
                costoFresatura = (double)((ConnectionInfo.FirstOrDefault()?.Fresa_Price_Contrp ?? 0) * (bda_code.TakeNumber));
            }
            //Costo marcatura
            int costoMarcatura = 5;
            //Costo tubo
            double costoTubo = (double)((DN_Sched_Tube.FirstOrDefault()?.Meter_Price ?? 0) * bda_code.Lung / 1000);
            //Costo taglio
            double costoTaglio = (double)(DN_Sched_Tube.FirstOrDefault()?.Cut_Price ?? 0);
            //Costo tornitura
            double costoTornitura = (double)(DN_Sched_Tube.FirstOrDefault()?.Torn_Price ?? 0m);
            //Costo Connessione
            double costoConnessione = (double)((ConnectionInfo.FirstOrDefault()?.Price ?? 0m) * bda_code.TakeNumber);
            //Costo Saldatura Circo
            double costoSaldaturaCirco = (double)(DN_Sched_Tube.FirstOrDefault()?.Circ_Sald_Price ?? 0);
            //Costo Saldatura Stacchi
            double costoSaldaturaStacchi = (double)((ConnectionInfo.FirstOrDefault()?.Sald_Stacchi_Price ?? 0) * bda_code.TakeNumber);
            //Costo Closure
            double costoClosure = (double)(DN_Sched_Tube.FirstOrDefault()?.Closure_Tot_Price ?? 0);
            //Costo Sabbiatura
            double CostoSabbiatura = (double)(db_tables.Sabb_Stac.Where(x => x.Stacchi == bda_code.TakeNumber && x.StacchiType == StacType).FirstOrDefault()?.Sabb_Price ?? 0); //Recupero da db prezzo sabbiatura associato al numero di stacchi
            //Costo Zincatura
            double costoZincatura = (double)((DN_Sched_Tube.FirstOrDefault()?.Zinc_KG_Price ?? 0) * (DN_Sched_Tube.FirstOrDefault()?.Linear_Weight ?? 0) * bda_code.Lung / 1000);

            double costoTot = costoTubo + costoTaglio + costoTornitura + costoConnessione + costoSaldaturaCirco + costoSaldaturaStacchi + costoFresatura + costoDrain + costoClosure + costoSaldaturaDrain + CostoSabbiatura + costoZincatura + costoMarcatura;
            return costoTot;
        }
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
