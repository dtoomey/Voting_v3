using System.Collections.Generic;
using System.Runtime.Serialization;

namespace VotingWeb.Models
{
    [DataContract]
    public class VoteTally
    {
        [DataMember]
        public List<KeyValuePair<string, int>> Votes { get; set; }

        [DataMember]
        public long TotalBallots { get; set; }
    }
}
