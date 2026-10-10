using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IscrizioniManager.Dtos
{
  public class IscrizioneExcelDto
  {
    // bambino
    public string? Nome { get; set; }
    public string? Cognome { get; set; }
    public DateTime DataNascita { get; set; }
    public string? LuogoNascita { get; set; }

    // genitori (max 2)
    public string? Gen1Nome { get; set; }
    public string? Gen1Cognome { get; set; }
    public string? Gen1Telefono { get; set; }

    public string? Gen2Nome { get; set; }
    public string? Gen2Cognome { get; set; }
    public string? Gen2Telefono { get; set; }

    // settimane (max 2)
    public string? Settimana1 { get; set; }
    public string? Settimana2 { get; set; }
  }

}
