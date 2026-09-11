#!/bin/bash
# Seed the SAME demo data as scripts/seed-demo.ps1, but against a running production
# deployment over HTTPS (for a final manual smoke test). Delete this script when done —
# it's not meant to stay in the repo long-term, and there's no "fresh db" undo button here
# like in the dev script, since this runs against real prod data.
#
# What this creates:
#   - 4 users, all with password "demo1234": demo, alice, bob, carol
#     (emails: demo@taletrack.dev, alice@taletrack.dev, bob@taletrack.dev, carol@taletrack.dev)
#   - demo: ~72 tracking items (12 curated books/movies/series + a 60-book catalog,
#     mostly "finished" so the library looks populated) — no reviews
#   - alice: 5 tracked items, avatar set, up to 3 reviews on finished ones
#   - bob:   5 tracked items, avatar set, up to 3 reviews on finished ones
#   - carol: 3 tracked items, avatar set, up to 3 reviews on finished ones
#   - friendships: demo<->alice accepted, demo<->bob accepted, alice<->bob accepted,
#     carol->demo left pending on purpose (to see the incoming-request UI)
#   - demo also gets an avatar + full sharing privacy set at the end
#
# To clean it up afterwards (run on the server, against the prod Postgres container):
#   docker compose exec -T postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c \
#     "DELETE FROM \"Users\" WHERE \"Email\" IN ('demo@taletrack.dev','alice@taletrack.dev','bob@taletrack.dev','carol@taletrack.dev');"
#   (Reviews and TrackingEvents cascade-delete with their user, per CLAUDE.md.)
#
# Requires: curl, jq (sudo apt install -y jq if missing)
# Usage:    ./scripts/seed-demo-prod.sh [https://taletrack.app/api]

set -e

API_BASE="${1:-https://taletrack.app/api}"
PASSWORD="demo1234"

echo "-> Using API: $API_BASE"

json_post() {
  local url=$1 body=$2 auth=$3
  if [ -n "$auth" ]; then
    curl -s -X POST "$url" -H "Content-Type: application/json" -H "Authorization: Bearer $auth" -d "$body"
  else
    curl -s -X POST "$url" -H "Content-Type: application/json" -d "$body"
  fi
}

json_put() {
  local url=$1 body=$2 auth=$3
  curl -s -X PUT "$url" -H "Content-Type: application/json" -H "Authorization: Bearer $auth" -d "$body"
}

json_get() {
  local url=$1 auth=$2
  curl -s "$url" -H "Authorization: Bearer $auth"
}

register() {
  local email=$1 username=$2
  local body
  body=$(jq -n --arg email "$email" --arg username "$username" --arg password "$PASSWORD" \
    '{email:$email, username:$username, password:$password}')
  json_post "$API_BASE/register" "$body" "" > /dev/null || true
}

login() {
  local email=$1
  local body
  body=$(jq -n --arg email "$email" --arg password "$PASSWORD" '{email:$email, password:$password}')
  json_post "$API_BASE/login" "$body" "" | jq -r '.token'
}

# type: Book | Movie | Series
add_tracking() {
  local auth=$1 type=$2 title=$3 progress=$4 length=$5 author=$6 isbn=$7
  case "$type" in
    Book)
      local body
      body=$(jq -n --arg title "$title" --argjson progress "$progress" \
        --arg author "$author" --arg isbn "$isbn" --argjson length "${length:-0}" '
        {title:$title, progress:$progress}
        + (if $author != "" then {author:$author} else {} end)
        + (if $isbn   != "" then {isbn:$isbn}     else {} end)
        + (if $length > 0   then {pages:$length}  else {} end)')
      json_post "$API_BASE/tracking/books" "$body" "$auth" > /dev/null
      ;;
    Movie)
      local body
      body=$(jq -n --arg title "$title" --argjson progress "$progress" --argjson length "${length:-0}" '
        {title:$title, progress:$progress}
        + (if $length > 0 then {minutes:$length} else {} end)')
      json_post "$API_BASE/tracking/movies" "$body" "$auth" > /dev/null
      ;;
    Series)
      local total_eps=8
      if [ -n "$length" ] && [ "$length" -le 24 ] 2>/dev/null; then total_eps=$length; fi
      local watched=$total_eps
      if [ "$progress" -lt 100 ]; then
        watched=$(( (total_eps * progress + 99) / 100 ))
        [ "$watched" -lt 1 ] && watched=1
      fi
      for ((ep=1; ep<=watched; ep++)); do
        local body
        body=$(jq -n --arg title "$title" --argjson season 1 --argjson episode "$ep" '
          {title:$title, season:$season, episode:$episode, minutes:45, progress:100}')
        json_post "$API_BASE/tracking/series" "$body" "$auth" > /dev/null
      done
      ;;
  esac
}

echo "-> Registering demo@taletrack.dev ..."
register "demo@taletrack.dev" "demo"

echo "-> Logging in ..."
DEMO_TOKEN=$(login "demo@taletrack.dev")
if [ -z "$DEMO_TOKEN" ] || [ "$DEMO_TOKEN" = "null" ]; then
  echo "Login failed for demo@taletrack.dev — check the API is reachable at $API_BASE" >&2
  exit 1
fi

# type;title;length;progress;author;isbn
items=(
  "Book;The Hobbit;310;100;J.R.R. Tolkien;9780547928227"
  "Book;Dune;688;64;Frank Herbert;9780441013593"
  "Book;Project Hail Mary;496;100;Andy Weir;9780593135204"
  "Book;The Name of the Wind;662;27;Patrick Rothfuss;9780756404741"
  "Book;Klara and the Sun;320;100;Kazuo Ishiguro;9780593318171"
  "Book;Piranesi;245;88;Susanna Clarke;9781635575637"
  "Movie;Blade Runner 2049;164;100;;"
  "Movie;Everything Everywhere All at Once;139;100;;"
  "Movie;Dune: Part Two;166;45;;"
  "Series;Severance;9;100;;"
  "Series;The Bear;10;70;;"
  "Series;Shogun;10;30;;"
)

book_catalog=(
  "The Fellowship of the Ring;J.R.R. Tolkien;423"
  "The Two Towers;J.R.R. Tolkien;352"
  "The Return of the King;J.R.R. Tolkien;416"
  "A Game of Thrones;George R.R. Martin;694"
  "A Clash of Kings;George R.R. Martin;768"
  "The Way of Kings;Brandon Sanderson;1007"
  "Mistborn: The Final Empire;Brandon Sanderson;541"
  "The Fifth Season;N.K. Jemisin;468"
  "A Wizard of Earthsea;Ursula K. Le Guin;205"
  "The Left Hand of Darkness;Ursula K. Le Guin;304"
  "Neuromancer;William Gibson;271"
  "Snow Crash;Neal Stephenson;468"
  "The Three-Body Problem;Liu Cixin;400"
  "Foundation;Isaac Asimov;244"
  "I, Robot;Isaac Asimov;224"
  "Do Androids Dream of Electric Sheep?;Philip K. Dick;210"
  "Hyperion;Dan Simmons;482"
  "Ender's Game;Orson Scott Card;324"
  "The Dispossessed;Ursula K. Le Guin;341"
  "Rendezvous with Rama;Arthur C. Clarke;256"
  "2001: A Space Odyssey;Arthur C. Clarke;297"
  "Ringworld;Larry Niven;342"
  "The Forever War;Joe Haldeman;278"
  "A Canticle for Leibowitz;Walter M. Miller Jr.;320"
  "Solaris;Stanislaw Lem;204"
  "Roadside Picnic;Arkady and Boris Strugatsky;209"
  "The Stars My Destination;Alfred Bester;258"
  "Childhood's End;Arthur C. Clarke;224"
  "The Martian;Andy Weir;369"
  "Recursion;Blake Crouch;336"
  "Dark Matter;Blake Crouch;342"
  "Station Eleven;Emily St. John Mandel;333"
  "The Road;Cormac McCarthy;287"
  "Never Let Me Go;Kazuo Ishiguro;288"
  "The Remains of the Day;Kazuo Ishiguro;258"
  "Cloud Atlas;David Mitchell;509"
  "The Night Circus;Erin Morgenstern;387"
  "Jonathan Strange & Mr Norrell;Susanna Clarke;782"
  "The Priory of the Orange Tree;Samantha Shannon;830"
  "Circe;Madeline Miller;393"
  "The Song of Achilles;Madeline Miller;352"
  "Gideon the Ninth;Tamsyn Muir;448"
  "The Poppy War;R.F. Kuang;545"
  "Babel;R.F. Kuang;546"
  "The City & the City;China Mieville;312"
  "Perdido Street Station;China Mieville;710"
  "American Gods;Neil Gaiman;465"
  "The Ocean at the End of the Lane;Neil Gaiman;181"
  "Good Omens;Neil Gaiman and Terry Pratchett;288"
  "Small Gods;Terry Pratchett;384"
  "Guards! Guards!;Terry Pratchett;355"
  "Mort;Terry Pratchett;272"
  "The Colour of Magic;Terry Pratchett;288"
  "A Deepness in the Sky;Vernor Vinge;774"
  "Blindsight;Peter Watts;384"
  "Children of Time;Adrian Tchaikovsky;600"
  "The Long Way to a Small, Angry Planet;Becky Chambers;518"
  "A Memory Called Empire;Arkady Martine;462"
  "This Is How You Lose the Time War;Amal El-Mohtar and Max Gladstone;209"
)

i=0
for line in "${book_catalog[@]}"; do
  IFS=';' read -r title author length <<< "$line"
  case $(( i % 7 )) in
    5) progress=100 ;;
    6) opts=(30 55 78); progress=${opts[$((RANDOM % 3))]} ;;
    *) progress=100 ;;
  esac
  items+=("Book;$title;$length;$progress;$author;")
  ((i++))
done

echo "-> Adding ${#items[@]} tracking events ..."
for item in "${items[@]}"; do
  IFS=';' read -r type title length progress author isbn <<< "$item"
  add_tracking "$DEMO_TOKEN" "$type" "$title" "$progress" "$length" "$author" "$isbn"
  printf "   + %-38s %-7s %s%%\n" "$title" "$type" "$progress"
done

echo ""
echo "-> Seeding friends (alice, bob, carol) ..."

avatar_url() { echo "https://api.dicebear.com/9.x/notionists/svg?seed=$1"; }
SHARE_ALL='{"bookProgress":true,"bookReviews":true,"movieProgress":true,"movieReviews":true,"seriesProgress":true,"seriesReviews":true}'

new_seed_user() {
  local name=$1 email=$2 seed=$3
  shift 3
  register "$email" "$name"
  local token
  token=$(login "$email")

  local me_id
  me_id=$(json_get "$API_BASE/user/me" "$token" | jq -r '.data.id')

  local avatar
  avatar=$(avatar_url "$seed")
  local body
  body=$(jq -n --arg avatarUrl "$avatar" --argjson privacy "$SHARE_ALL" '{avatarUrl:$avatarUrl, privacy:$privacy}')
  json_put "$API_BASE/user/$me_id" "$body" "$token" > /dev/null

  local tracked=0
  for spec in "$@"; do
    IFS='|' read -r title type progress <<< "$spec"
    [ -z "$progress" ] && progress=100
    local length=300
    [ "$type" = "Series" ] && length=8
    add_tracking "$token" "$type" "$title" "$progress" "$length" "" ""
    ((tracked++))
  done

  local rated=0
  local lib
  lib=$(json_get "$API_BASE/library?status=finished&limit=50" "$token")
  local media_ids
  media_ids=$(echo "$lib" | jq -r '.data[].mediaId' | head -3)
  local ratings=(6 7 8 9 10)
  while IFS= read -r media_id; do
    [ -z "$media_id" ] && continue
    local rating=${ratings[$((RANDOM % 5))]}
    local rbody
    rbody=$(jq -n --arg mediaId "$media_id" --argjson rating "$rating" --arg comment "Loved this one." \
      '{mediaId:$mediaId, rating:$rating, comment:$comment}')
    json_post "$API_BASE/review" "$rbody" "$token" > /dev/null
    ((rated++))
  done <<< "$media_ids"

  echo "   + @$name  ($tracked tracked, $rated reviewed)"
  echo "$token"
}

ALICE_TOKEN=$(new_seed_user "alice" "alice@taletrack.dev" "Aneka" \
  "The Left Hand of Darkness|Book|100" "Piranesi|Book|100" \
  "Dune|Book|60" "Arrival|Movie|100" "Severance|Series|100")

BOB_TOKEN=$(new_seed_user "bob" "bob@taletrack.dev" "Felix" \
  "Project Hail Mary|Book|100" "The Martian|Book|100" \
  "Blade Runner 2049|Movie|100" "Foundation|Series|45" "Neuromancer|Book|100")

CAROL_TOKEN=$(new_seed_user "carol" "carol@taletrack.dev" "Luna" \
  "Circe|Book|100" "The Song of Achilles|Book|100" "The Bear|Series|80")

send_friend_req() {
  local from_token=$1 to_username=$2
  local found
  found=$(json_get "$API_BASE/users/search?username=$to_username" "$from_token")
  local user_id
  user_id=$(echo "$found" | jq -r '.user.userId // empty')
  [ -z "$user_id" ] && return 0
  local body
  body=$(jq -n --arg userId "$user_id" '{userId:$userId}')
  json_post "$API_BASE/friends/requests" "$body" "$from_token" > /dev/null || true
}

accept_friend_req() {
  local as_token=$1 from_username=$2
  local f req_id
  f=$(json_get "$API_BASE/friends" "$as_token")
  req_id=$(echo "$f" | jq -r --arg u "$from_username" '.incoming[] | select(.username == $u) | .requestId' | head -1)
  [ -z "$req_id" ] && return 0
  json_post "$API_BASE/friends/requests/$req_id" '{"accept":true}' "$as_token" > /dev/null
}

send_friend_req "$DEMO_TOKEN" "alice";  accept_friend_req "$ALICE_TOKEN" "demo"
send_friend_req "$BOB_TOKEN" "demo";    accept_friend_req "$DEMO_TOKEN" "bob"
send_friend_req "$ALICE_TOKEN" "bob";   accept_friend_req "$BOB_TOKEN" "alice"
send_friend_req "$CAROL_TOKEN" "demo"   # left pending on purpose
echo "   friendships: demo<->alice, demo<->bob, alice<->bob  |  carol->demo pending"

demo_id=$(json_get "$API_BASE/user/me" "$DEMO_TOKEN" | jq -r '.data.id')
demo_avatar=$(avatar_url "Milo")
demo_body=$(jq -n --arg avatarUrl "$demo_avatar" --argjson privacy "$SHARE_ALL" '{avatarUrl:$avatarUrl, privacy:$privacy}')
json_put "$API_BASE/user/$demo_id" "$demo_body" "$DEMO_TOKEN" > /dev/null

echo ""
echo "Done. Log in at ${API_BASE%/api}/login"
echo "  demo / alice / bob / carol  —  password: $PASSWORD"
