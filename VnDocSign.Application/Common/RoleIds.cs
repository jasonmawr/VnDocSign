using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Application.Common
{
    /// <summary>
    /// GUID cố định cho các vai trò (đồng bộ với DbSeeder).
    /// Khi đổi GUID ở môi trường thật, chỉ cần cập nhật DbSeeder & file này.
    /// </summary>
    public static class RoleIds
    {
        public static readonly Guid Admin = Guid.Parse("0c886d54-0d2f-42be-8897-cc706f526b83");
        public static readonly Guid VanThu = Guid.Parse("69644ec8-c624-485e-bbd8-a87f21776d68");
        public static readonly Guid ChuyenVien = Guid.Parse("6be0710a-aa0f-4ca5-836d-6fc337778fb8");

        public static readonly Guid TruongPhong = Guid.Parse("51664cdf-498f-4eeb-af3d-90000687c544");
        public static readonly Guid PhoPhong = Guid.Parse("460839a9-5c43-468b-89c7-6cd9ed681f1f");

        public static readonly Guid TruongKhoa = Guid.Parse("a9546fcb-8703-4bc0-b883-3901cee6880e");
        public static readonly Guid PhoKhoa = Guid.Parse("4cec07d6-fcc9-4ded-b950-93d7f45ee108");

        public static readonly Guid KeToanTruong = Guid.Parse("df828b33-7f52-4ada-b6c3-9fb3edc2f391");

        public static readonly Guid PhoGiamDoc = Guid.Parse("8bbd11bd-8182-4b4a-8202-87cbab3c916e");
        public static readonly Guid GiamDoc = Guid.Parse("545b7d06-4c0f-49b0-9ec2-03b944a23125");

        public static readonly Guid ChuTichCongDoan = Guid.Parse("032f5bd0-3329-4597-a9c8-1ec0521945ec");
        public static readonly Guid DieuDuongTruong = Guid.Parse("cb83d980-1566-4a7f-896e-73f08b48fb97");
        public static readonly Guid KTVTruong = Guid.Parse("1a5a1096-d91e-4c64-a134-4ffa96343172");
    }
}
