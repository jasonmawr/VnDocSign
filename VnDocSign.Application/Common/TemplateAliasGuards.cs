using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Application.Common
{
    public static class TemplateAliasGuards
    {
        //Chỉ "Văn thư" được ghi và chỉ ở thời điểm cho phép
        public static readonly HashSet<string> ClerkOnlyAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            "SO_LUU_TRU",
            "NGAY_LUU_TRU"
        };
    }
}
