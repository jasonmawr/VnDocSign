using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Application.Common;

public static class TemplateAliasMaps
{
    //KEY FE -> ALIAS DOCX
    public static readonly IReadOnlyDictionary<string, string> Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["donVi"] = "DON_VI",
        ["vuViec"] = "VU_VIEC",
        ["kinhGui"] = "KINH_GUI",
        ["canCuPhapLy"] = "CANCU_PHAPLY",
        ["noiDungTrinh"] = "NOIDUNG_TRINH",
        ["kienNghiDeXuat"] = "KIENNGHI_DEXUAT",
        // mở rộng khi cần
        ["soLuuTru"] = "SO_LUU_TRU",
        ["ngayLuuTru"] = "NGAY_LUU_TRU",
    };
}
