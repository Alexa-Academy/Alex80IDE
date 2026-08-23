using Konamiman.Nestor80.Assembler.Expressions;

namespace Konamiman.Nestor80.Assembler.Relocatable
{
    internal class Address : IExpressionPart
    {
        public static Address AbsoluteZero = new(AddressType.ASEG, 0);
        public static Address AbsoluteMinusOne = new(AddressType.ASEG, 0xFFFF);

        /// <summary>
        /// Ampiezza dello spazio di indirizzamento della CPU corrente:
        /// 0xFFFF per Z80, R800 e Z280, 0xFFFFFF per eZ80.
        /// Ogni valore viene mascherato qui, cosi' il troncamento resta in un
        /// punto solo e lo Z80 mantiene esattamente il wraparound a 64 KB che
        /// aveva quando Value era uno ushort.
        /// </summary>
        public static uint ValueMask { get; set; } = 0xFFFF;

        /// <summary>Vero quando lo spazio di indirizzamento e' a 24 bit.</summary>
        public static bool IsWideAddressSpace => ValueMask > 0xFFFF;

        public Address(AddressType type, uint value, string commonBlockName = null)
        {
            Type = type;
            Value = value & ValueMask;
            CommonBlockName = commonBlockName;
        }

        public static Address Absolute(uint value) => new(AddressType.ASEG, value);
        public static Address Code(uint value) => new(AddressType.CSEG, value);
        public static Address Data(uint value) => new(AddressType.DSEG, value);

        public static bool IsSdccBuild { get; set; }

        public string EffectiveType =>
            IsSdccBuild ? (Type is AddressType.ASEG ? "absolute" : "relocatable") : Type.ToString();

        public AddressType Type { get; private set; }

        public uint Value { get; private set; }

        public string CommonBlockName { get; private set; }

        public bool IsAbsolute => Type == AddressType.ASEG;
        public bool IsCommon => Type == AddressType.COMMON;

        public bool SameModeAs(Address address2)
        {
            return Type == address2.Type;
        }

        public bool IsValidByte
        {
            get
            {
                // Il secondo ramo accetta i valori negativi in complemento a
                // due sull'ampiezza corrente (0xFF00..0xFFFF a 16 bit,
                // 0xFFFF00..0xFFFFFF a 24 bit).
                return Value < 256 || Value >= (ValueMask & ~0xFFu);
            }
        }

        public byte ValueAsByte
        {
            get
            {
                return (byte)(Value & 0xFF);
            }
        }

        public override string ToString()
        {
            return $"{EffectiveType} {Value:X4}";
        }

        public static bool operator ==(Address address1, object address2)
        {
            if (address2 is not Address)
                return false;

            if (address1 is null)
                return address2 is null;

            return address1.Equals(address2);
        }

        public static bool operator !=(Address address1, object address2)
        {
            return !(address1 == address2);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            var b2 = (Address)obj;
            return Type == b2.Type && Value == b2.Value && (!IsCommon || CommonBlockName == b2.CommonBlockName);
        }

        public override int GetHashCode()
        {
            var result = (int)Value ^ ((int)Type << 24);
            if (CommonBlockName is not null)
            {
                result ^= CommonBlockName.GetHashCode();
            }
            return result;
        }
    }
}
