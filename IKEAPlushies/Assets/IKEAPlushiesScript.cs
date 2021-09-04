using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;

public class IKEAPlushiesScript : MonoBehaviour {

    public KMBombInfo Bomb;
    public KMAudio Audio;

    public KMSelectable[] buttons;
    public KMSelectable collector;
    public SpriteRenderer[] sprites;
    public Sprite[] allSprites;

    private string[] grids = new string[]
        { "x-------xx---x-", "----xx---x----x", "--x--xx-----x--", "-x----xx-----x-", "----x-x--x---x-", "----xxx----x---", "-x-----x-x----x", "--x--xx------x-", "x---x------xx--", "---x----xx-x---"};
    private string chosenGrid;
    private string[] directions = new string[] { "NW", "NE", "SW", "SE" };

    int startingPos;
    int currentPos;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    int stage = 0;
    int[] plushiePositions = new int[4];
    List<int> collectedPlushies = new List<int>();

    int[] SNNumbers;
    int[] plushieValues;
    int[] chosenPlushies;
    int opposite;


    void Awake() 
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable button in buttons)
            button.OnInteract += delegate () { ButtonPress(Array.IndexOf(buttons, button)); return false; };
        collector.OnInteract += delegate () { Collect(); return false; };
    }

    void Start()
    {
        SNNumbers = Bomb.GetSerialNumberNumbers().ToArray();
        DoCalcs();
        GetPlushies();
    }

    void DoCalcs()
    {
        chosenGrid = grids[SNNumbers.First()];
        startingPos = KeepOutOfTrap(SNNumbers.Sum() + 14);
        currentPos = startingPos;
        Debug.LogFormat("[IKEA Plushies #{0}] The grid being used by the module is grid {1}.", moduleId, SNNumbers.First());
        Debug.LogFormat("[IKEA Plushies #{0}] The starting position is position {1}.", moduleId, startingPos + 1);
        int[] abcd = SNNumbers.Select(x => x == 0 ? 10 : x).ToArray();
        int A = abcd[0];
        int B = abcd[1];
        int C, D;
        switch (SNNumbers.Count())
        {
            case 2:
                plushieValues = new int[] { A+B, A-B, A+2*B, A*B, A+B+7, A+B-3, A*B-10, 3*A-B, A*A+B*B, 2*A-B, 3*A+3*B, A*A-B*B };
                break;
            case 3:
                C = abcd[2];
                plushieValues = new int[] { A*B+C, A+B+C, A*B*C, A+B*C, A+B-C, (A-B)*(A-B)+C, A-B+C, A+2*B-3*C, A*A+B*B+C*C, A-B-C, 2*A+2*B-C, A-B*C };
                break;
            case 4:
                C = abcd[2];
                D = abcd[3];
                plushieValues = new int[] { A*B+C*D, A+B+C+D, A-B+C-D, (A+B+C)*D, A*(B-C)*D, A*B-C*D, A*A+B*B+C*D, A-8+B*C*D, A*A+B*B-C*C-D*D, A*B-C+D, A+2*B+3*C+4*D, A*B+C-D };
                break;
        }
        plushieValues = plushieValues.Select(x => KeepOutOfTrap(KeepInRange(x - 1))).ToArray();

    }

    void GetPlushies()
    {
        chosenPlushies = Enumerable.Range(0,12).ToArray().Shuffle().Take(4).ToArray();
        plushiePositions = chosenPlushies.Select(x => plushieValues[x]).ToArray();
        for (int i = 0; i < 4; i++)
            sprites[i].sprite = allSprites[chosenPlushies[i]];
        Debug.LogFormat("[IKEA Plushies #{0}] The chosen plushies are {1}.", moduleId, sprites.Select(x => x.sprite.name).Join(", "));
        Debug.LogFormat("[IKEA Plushies #{0}] The plushies are at positions {1}.", moduleId, plushiePositions.Select(x => x + 1).Join(", "));

    }

    void ButtonPress(int direction)
    {
        buttons[direction].AddInteractionPunch(0.75f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, buttons[direction].transform);
        if (moduleSolved) return;

        int endPos = DiagonalMove(currentPos, direction);
        Debug.Log(endPos);
        if (chosenGrid[endPos] == 'x')
        {
            Debug.LogFormat("[IKEA Plushies #{0}] Attempted to move {1} from position {2} to {3}, which is on a trap tile. Strike incurred and positions reset.", 
                moduleId, directions[direction], currentPos + 1, endPos + 1);
            currentPos = startingPos;
            collectedPlushies.Clear();
            stage = 0;
            GetComponent<KMBombModule>().HandleStrike();
        }
        else
        {
            Debug.LogFormat("[IKEA Plushies #{0}] Moved {1} from position {2} to {3}.", moduleId, directions[direction], currentPos + 1, endPos + 1);
            currentPos = endPos;
            opposite = 3 - direction;
        }
    }

    void Collect()
    {
        collector.AddInteractionPunch(1);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, collector.transform);
        if (moduleSolved)
            return;

        if (!plushiePositions.Any(x => x == currentPos))
        {
            Debug.LogFormat("[IKEA Plushies #{0}] Collect button pressed on a space with no plushies. Strike.", moduleId);
            GetComponent<KMBombModule>().HandleStrike();
            return;
        }
        for (int i = 0; i < 4; i++)
        {
            if (!collectedPlushies.Contains(i) && currentPos == plushiePositions[i])
            {
                Debug.LogFormat("[IKEA Plushies #{0}] Collected the {1} at position {2}.", moduleId, sprites[i].sprite.name, currentPos + 1);
                stage++;
                collectedPlushies.Add(i);
            }
        }
        if (stage == 4)
        {
            moduleSolved = true;
            Debug.LogFormat("[IKEA Plushies #{0}] All four plushies collected. Module solved.", moduleId);
            GetComponent<KMBombModule>().HandlePass();
            Audio.PlaySoundAtTransform((UnityEngine.Random.Range(0, 100) == 0 || Environment.MachineName == "DESKTOP-RGTP319" || Environment.MachineName == "DESKTOP-4HOQP30")
                                        ? "gloop" : "geck", transform);
        }
    }


    int KeepInRange(int input)
    {
        return (input % 15 + 15) % 15;
    }

    int KeepOutOfTrap(int start)
    {
        int pos = start % 15;
        while (chosenGrid[pos] == 'x')
            pos = (pos + 4) % 15;
        return pos;
    }

    int DiagonalMove(int start, int direction)
    {
        int rowChange = (direction < 2) ? 4 : 1; //4 = -1 (mod 5)
        int colChange = (direction % 2 == 0) ? 2 : 1; //2 = -1 (mod 3)
        return 3*(((start / 3) + rowChange) % 5)  +  (((start % 3) + colChange) % 3);
    }

    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use [!{0} press NW TR 3 SE] to press those buttons. Use [!{0} press collect] to press the collect button. Commands can be chained with spaces; collect commands can be interspersed within movement commands.";
    #pragma warning restore 414

    IEnumerator ProcessTwitchCommand (string input)
    {
        string[] positions = new string[] { "NW", "NE", "SW", "SE", "TL", "TR", "BL", "BR", "1", "2", "3", "4", "COLLECT", "SUBMIT" };
        List<string> parameters = input.Trim().ToUpperInvariant().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parameters.First() == "PRESS" || parameters.First() == "MOVE")
        {
            parameters.Remove(parameters.First());
            if (parameters.All(x => positions.Contains(x)))
            {
                yield return null;
                foreach (string movement in parameters)
                {
                    if (movement == "COLLECT" || movement == "SUBMIT")
                    {
                        collector.OnInteract();
                        yield return new WaitForSeconds(0.15f);
                    }
                    else
                    {
                    buttons[Array.IndexOf(positions, movement) % 4].OnInteract();
                    yield return new WaitForSeconds(0.15f);
                    }
                }
            }
            else yield return "sendtochaterror Invalid button " + parameters.First(x => !positions.Contains(x));
        }
    }
}
