using Miner.Models;
using Priority_Queue;

namespace Miner
{
    public class MyNode : FastPriorityQueueNode
    {
        public Explore Report { get; set; }

        public int Depth { get; set; } = 1;

        public float CalculatePriority()
        {
            return -this.Report.Amount * 1.0f / (this.Report.Area.SizeX * this.Report.Area.SizeY);
        }

        public bool IsCell()
        {
            return this.Report.Area.SizeX == 1 && this.Report.Area.SizeY == 1;
        }

        public bool IsEmpty()
        {
            return this.Report.Amount == 0;
        }
    }
}