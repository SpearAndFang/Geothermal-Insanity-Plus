namespace GeothermalInsanity.ModSystem
{
    using Vintagestory.API.Common;
    using System;
    using Vintagestory.API.MathTools;
    using Vintagestory.API.Client;
    using System.Diagnostics;
    using Vintagestory.API.Server;
    using Vintagestory.GameContent;

    public class GeothermalInsanitySystem : ModSystem
    {
        

        public override double ExecuteOrder()
        {
            //has it execute after in-game engine fuckery to override variations
            return 999;
        }

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            //quixjote's key to make it actually fucking work
            base.Start(api);
            //some witchcraft from goxmeor to start this all
            api.Event.OnGetClimate += (ref ClimateCondition climate, BlockPos pos, EnumGetClimateMode mode, double totalDays) =>
            {
                //define some shit we need for intermediary
                float subDepth = 0, blockAbove = 0, genDifference = 0;
                var isUnderground = false;
                var rainMapHeight = api.World.BlockAccessor.GetRainMapHeightAt(pos);
                var genHeight = api.World.BlockAccessor.GetTerrainMapheightAt(pos);
                //checks below gen height, assigns level difference
                if (pos.Y < genHeight)
                {
                    genDifference = genHeight - pos.Y;
                }
                //attempts to figure out if there's an impermeable block somewhere above your head (are you under cover?)
                if (pos.Y < rainMapHeight)
                {
                    blockAbove = rainMapHeight - pos.Y;
                }
                //checks to confirm both below surface and below gen height (properly underground)
                if (genDifference > 2 && blockAbove > 2)
                {
                    isUnderground = true;
                }
                //underground? how deep below surface and sea level?
                if (isUnderground)
                {
                    subDepth = Math.Min(genDifference, blockAbove) + 2;
                }
                //doing the actual temperature shifts
                if (subDepth > 0)
                {
                    climate.Temperature = 5 + (subDepth / 2.5f);
                }
            };
        }


        //CHANGES START NOW!

        //we'll need to access the server api in our listener method (PlayerTick)
        //which is why it's initialized outside of the StartServerSide method
        ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            // initialize the server api variable that's declared above
            this.sapi = api;

            // how often to check and potentially adjust player temperatures
            // that's irl ms if I'm not mistaken
            var checkFrequency = 2000;

            // now create a listener. which will run the PlayerTick method (below)
            // every checkFrequency milliseconds
            api.World.RegisterGameTickListener(new Action<float>(this.PlayerTick), checkFrequency, 0);
        }


        public void PlayerTick(float par)
        {
            //note: this only works in survival mode, NOT creative

            //loop for every active player on the interval
            foreach (var player in this.sapi.World.AllOnlinePlayers)
            {
                //get a player
                var splayer = player as IServerPlayer;
                //confirm thay are playing
                if (player.Entity != null && splayer.ConnectionState == EnumClientState.Playing)
                {
                    //confirm they are in water
                     if (player.Entity.Swimming)
                    {
                        //get player position and convert it to a nearby block position
                        var pos = player.Entity.ServerPos.AsBlockPos;
                        //get the climate at that block position
                        var climate = this.sapi.World.BlockAccessor.GetClimateAt(pos);
                        //get the in world temperature at that block position
                        var temp = climate.Temperature; 
                        //get the behavior that tracks the player's body temperature
                        var bh = player.Entity.GetBehavior<EntityBehaviorBodyTemperature>();
                        if (bh != null)
                        {
                            // calculate the intensity of the body temperature change
                            //based on world temperature (temp) and current body temperature
                            var intensity = Math.Min(0, (temp - bh.CurBodyTemperature) / 100f);

                            //calculate the exact amount we will modify the body temperature
                            //by, using some gamemath to clamp it to a certain range
                            var adj = GameMath.Clamp(intensity, -0.2f, 0f);

                            //those two calculations are pretty loosey goosey because
                            //I'm not learned in the ways of water temp and body temp

                            //let's see those numbers in the debugger
                            //Debug.WriteLine("intensity:" + intensity);
                            //Debug.WriteLine("adj:" + adj);

                            //actually change that player's body temperature
                            bh.CurBodyTemperature += adj;
                        }
                    }
                }
            }
        }
        //END OF CHANGES
    }
}
