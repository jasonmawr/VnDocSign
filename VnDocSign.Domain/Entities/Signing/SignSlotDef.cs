namespace VnDocSign.Domain.Entities.Signing;

public enum SlotPhase { Vung1 = 1, Vung2 = 2, Vung3 = 3, Clerk = 4, Director = 5 }

public enum SlotKey
{
    // Vùng 1
    NguoiTrinh, LanhDaoPhong, DonViLienQuan, // optional
    // Vùng 2: 5 phòng chức năng cố định
    KHTH, HCQT, TCCB, TCKT, CTCD,
    // Vùng 3: 3 PGĐ
    PGD1, PGD2, PGD3,
    // Văn thư & Giám đốc
    VanThuCheck, GiamDoc
}

public sealed class SignSlotDef
{
    public int Id { get; set; }                 // seed cố định 1..N
    public SlotKey Key { get; set; }
    public SlotPhase Phase { get; set; }
    public int OrderInPhase { get; set; }       // nếu phase tuần tự
    public bool Optional { get; set; }          // DonViLienQuan = true
}
