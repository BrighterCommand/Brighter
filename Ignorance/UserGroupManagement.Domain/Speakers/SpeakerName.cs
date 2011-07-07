namespace UserGroupManagement.Domain.Speakers
{
    public class SpeakerName
    {
        public string Name { get; private set;}

        public SpeakerName(string name)
        {
            this.Name = name;
        }
    }
}