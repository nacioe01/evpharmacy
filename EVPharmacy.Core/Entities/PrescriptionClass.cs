
namespace EVPharmacy.Core.Entities;

public enum PrescriptionClass
{
    /// <summary>Over the counter. No prescription required.</summary>
    OverTheCounter = 0,

    /// <summary>Prescription required.</summary>
    Prescription = 1,

    /// <summary>Dangerous or regulated drug. Prescription and special records required.</summary>
    Controlled = 2
}
