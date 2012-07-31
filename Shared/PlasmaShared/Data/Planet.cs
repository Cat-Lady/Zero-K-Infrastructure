﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ZkData
{
	partial class Planet
	{
		public const double OverlayRatio = 2.25;

		public IEnumerable<PlanetFaction> GetFactionInfluences()
		{
			return PlanetFactions.Where(x=>x.Influence > 0).OrderByDescending(x => x.Influence);
		}

     
        public Faction GetAttacker(IEnumerable<int> presentFactions) {
            return PlanetFactions.Where(x => presentFactions.Contains(x.FactionID) && x.FactionID != OwnerFactionID && x.Dropships > 0).OrderByDescending(x => x.Dropships).ThenBy(x => x.DropshipsLastAdded).Select(x => x.Faction).FirstOrDefault();
        }

	    public override string ToString() {
	        return Name;
	    }

	    public string GetColor(Account viewer)
		{
            if (Account == null || Account.Faction == null) return "#808080";
            else return Account.Faction.Color;
		}


		public Rectangle PlanetOverlayRectangle(Galaxy gal)
		{
			var w = Resource.PlanetWarsIconSize*OverlayRatio;
			var xp = (int)(X*gal.Width);
			var yp = (int)(Y*gal.Height);
			return new Rectangle((int)(xp - w/2), (int)(yp - w/2), (int)w, (int)w);
		}

		public Rectangle PlanetRectangle(Galaxy gal)
		{
			var w = Resource.PlanetWarsIconSize;
			var xp = (int)(X*gal.Width);
			var yp = (int)(Y*gal.Height);
			return new Rectangle((int)(xp - w/2), (int)(yp - w/2), (int)w, (int)w);
		}

	    public int GetUpkeepCost()
	    {
            return PlanetStructures.Sum(y => (int?)y.StructureType.UpkeepEnergy)??0;
	    }

        public bool CanDropshipsAttack(Faction attacker) {
            return CheckLinkAttack(attacker, x => x.EffectPreventDropshipAttack == true, x => x.EffectAllowDropshipPass == true);
        }

        public bool CanDropshipsWarp(Faction attacker)
        {
            return CheckWarpAttack(attacker, x => x.EffectPreventDropshipAttack == true);
        }

        public bool CanBombersAttack(Faction attacker)
        {
            return CheckLinkAttack(attacker, x => x.EffectPreventBomberAttack == true, x => x.EffectAllowBomberPass == true);
        }

        public bool CanBombersWarp(Faction attacker)
        {
            return CheckWarpAttack(attacker, x => x.EffectPreventBomberAttack == true);
        }


        public bool CheckLinkAttack(Faction attacker, Func<TreatyEffectType, bool> preventingTreaty, Func<TreatyEffectType, bool> passageTreaty) {

            if (Faction != null && (OwnerFactionID == attacker.FactionID || Faction.HasTreatyRight(attacker, preventingTreaty, this))) return false; // attacker allied cannot strike

            if (Faction == null && !attacker.Planets.Any()) return true; // attacker has no planets, planet neutral, allow strike


            // iterate links to this planet
            foreach (var link in LinksByPlanetID1.Union(LinksByPlanetID2))
            {
                var otherPlanet = PlanetID == link.PlanetID1 ? link.PlanetByPlanetID2 : link.PlanetByPlanetID1;

                // planet has wormhole active
                if (otherPlanet.PlanetStructures.Any(x => x.IsActive && x.StructureType.EffectAllowShipTraversal == true))
                {

                    // planet belongs to attacker or person who gave attacker rights to pass
                    if (otherPlanet.Faction != null && (otherPlanet.OwnerFactionID == attacker.FactionID || attacker.HasTreatyRight(otherPlanet.Faction, passageTreaty, otherPlanet)))
                    {
                        return true;

                    }
                }
            }
            return false;

        }


        public bool CheckWarpAttack(Faction attacker, Func<TreatyEffectType, bool> preventingTreaty)
        {

            if (Faction != null && (OwnerFactionID == attacker.FactionID || Faction.HasTreatyRight(attacker, preventingTreaty, this))) return false; // attacker allied cannot strike
            if (PlanetStructures.Any(x => x.IsActive && x.StructureType.EffectBlocksJumpgate == true)) return false; // inhibitor active
            
            return true;
        }

	}
}