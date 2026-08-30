// Shared music theory logic will go here

export const NOTE_NAMES = [
  'C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'
];

export function getFifth(note: string): string {
  const idx = NOTE_NAMES.indexOf(note);
  if (idx === -1) return note;
  return NOTE_NAMES[(idx + 7) % 12];
}

const RELATIVE_MINOR_MAP: Record<string, string> = {
  'C': 'A', 'G': 'E', 'D': 'B', 'A': 'F#', 'E': 'C#', 'B': 'G#',
  'F#': 'D#', 'Db': 'Bb', 'Ab': 'F', 'Eb': 'C', 'Bb': 'G', 'F': 'D',
  // enharmonics for completeness
  'F♯': 'D#', 'D♭': 'Bb', 'A♭': 'F', 'E♭': 'C', 'B♭': 'G',
};

export function getRelativeMinor(note: string): string {
  // Accept both "F#" and "F♯", etc.
  return RELATIVE_MINOR_MAP[note.replace('♯', '#').replace('♭', 'b')] || '-';
}

export function getTriad(note: string, mode: "major" | "minor" = "major"): string[] {
  // Find the triad notes for a given root and mode
  // Major: 1, 3, 5; Minor: 1, b3, 5
  const idx = NOTE_NAMES.findIndex(
    n => n === note || n.replace('#', '♯') === note || n.replace('b', '♭') === note
  );
  if (idx === -1) return [];
  const third = mode === "major" ? (idx + 4) % 12 : (idx + 3) % 12;
  const fifth = (idx + 7) % 12;
  return [
    NOTE_NAMES[idx],
    NOTE_NAMES[third],
    NOTE_NAMES[fifth]
  ];
}
