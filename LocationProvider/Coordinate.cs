using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocationProvider
{
    struct Coordinate
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public Coordinate(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        public Coordinate() : this(0.0, 0.0) { }

        public bool IsValid()
        {
            return Latitude >= -90 && Latitude <= 90 && Longitude >= -180 && Longitude <= 180;
        }

        public static bool operator ==(Coordinate lhs, Coordinate rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(Coordinate lhs, Coordinate rhs)
        {
            return !(lhs == rhs);
        }

        public override bool Equals(object obj)
        {
            if (GetType() != obj.GetType())
                return false;

            var o = (Coordinate)obj;
            return o.Latitude == Latitude && o.Longitude == Longitude;
        }

        public override int GetHashCode()
        {
            return Latitude.GetHashCode() ^ Longitude.GetHashCode();
        }
    }
}
