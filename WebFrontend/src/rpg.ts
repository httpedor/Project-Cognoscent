export interface Vector2 {
  x: number
  y: number
}

export interface Vector3 {
  x: number
  y: number
  z: number
}

export interface StatModifier {
  id: string
  value: number
  type: 'Flat' | 'FlatPostMods' | 'Percent' | 'Multiplier' | 'Capmax' | 'Capmin' | 'OverrideBase' | 'OverrideFinal'
}

export interface Stat {
  id: string
  baseValue: number
  finalValue: number
  modifiers: { [key: string]: StatModifier }
}

export interface Feature {
  name: string
  description: string
  iconName: string
}

export interface Skill {
  name: string
  description: string
  iconName: string
}

export enum EntityType {
  Creature = 0,
  Item = 1,
  Projectile = 2,
  Door = 3,
  Container = 4,
  Light = 5,
  Prop = 6,
}

export interface Entity {
  id: number
  name?: string
  owner?: string
  display?: string // Base64-encoded image string
  position: Vector3
  rotation: number
  size: Vector3
  stats: { [key: string]: Stat }
  features: { [key: string]: { feature: Feature; enabled: boolean } }
  entityType: EntityType
}

export interface Board {
  name: string
  entities: Entity[]
  chatHistory: string[]
  currentTick: number
  turnMode: boolean
}
