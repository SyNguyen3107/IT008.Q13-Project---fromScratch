﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT008.Q13_Project___fromScratch.Models
{
    public class Deck
    {
        public int ID { get; private set; }
        public string Name { get; set; } = " ";
        public string Description { get; set; } = " ";
        public List<Card> Cards { get; set; } = new List<Card>();
    }
}
