import { reactive } from "vue"
import type { Midia } from "./networking.generated"
import type { Feature, Skill } from "./rpg"

export type CompendiumFolder = 'Skills' | 'Features' | 'Bodies' | 'Items' | 'SkillTrees' | 'Midia'

export type Compendium = {
  'Skills': { [id: string]: Skill }
  'Features': { [id: string]: Feature }
  'Bodies': { [id: string]: unknown }
  'Items': { [id: string]: unknown }
  'SkillTrees': { [id: string]: unknown }
  'Midia': { [id: string]: Midia }
}

export const compendium = reactive<Compendium>({
  Skills: {},
  Features: {},
  Bodies: {},
  Items: {},
  SkillTrees: {},
  Midia: {},
});
