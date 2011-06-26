namespace UserGroupManagement.Domain.Momentos
{
    public class LocationName
    {
        public string Name { get; private set; }

        public LocationName(string locationName)
        {
            Name = locationName;
        }
    }
}