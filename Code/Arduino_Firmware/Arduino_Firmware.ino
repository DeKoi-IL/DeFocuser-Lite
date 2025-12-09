#include <TMCStepper.h>
#include <SoftwareSerial.h>
#include <EEPROM.h>

#define DEBUG OFF

#define MCU ESP32C3

#if MCU == ESP32C3
  #define EN_PIN    10  // Enable
  #define DIR_PIN   7   // Direction
  #define STEP_PIN  6   // Step
  #define DIAG_PIN  4   // Stall detection
  #define SW_RX 20
  #define SW_TX 21
  #define BUTTON_PIN 9
#elif MCU == ESP32S3
  #define EN_PIN    9  // Enable
  #define DIR_PIN   6   // Direction
  #define STEP_PIN  5   // Step
  #define DIAG_PIN  3   // Stall detection
  #define SW_RX 44
  #define SW_TX 43
  #define BUTTON_PIN 8
#endif

#define DRIVER_SERIAL_BAUD 57600
#define STALL_COUNT_THRESHOLD 2
#define STALL_TIME_THRS 300
#define STALL_GRACE_PERIOD 1000

#define MAX_SPEED_DELAY 10000
#define MIN_SPEED_DELAY 14000

#define ACCELERATION_TIME 500.0

#define DRIVER_ADDRESS 0b00  // TMC2209 address pin configuration
#define R_SENSE 0.22f        // Sense resistor value

//-- Constants ----------------------------------------------------------------

constexpr auto DEVICE_GUID = "dfafe960-d19c-4abd-af4a-4dc5f49775a3";

constexpr auto MSG_OK = "OK";
constexpr auto MSG_NOK = "NOK";

constexpr auto TRUE = "TRUE";
constexpr auto FALSE = "FALSE";

constexpr auto COMMAND_PING = "COMMAND:PING";
constexpr auto RESULT_PING = "RESULT:PING:OK:";

constexpr auto COMMAND_INFO = "COMMAND:INFO";
constexpr auto RESULT_INFO = "RESULT:INFO:DeKoi's DeFocuser Lite Firmware v1.0";

constexpr auto COMMAND_FOCUSER_GETPOSITION = "COMMAND:FOCUSER:GETPOSITION";
constexpr auto RESULT_FOCUSER_POSITION = "RESULT:FOCUSER:POSITION:";

constexpr auto COMMAND_FOCUSER_GETMAXPOSITION = "COMMAND:FOCUSER:GETMAXPOSITION";
constexpr auto RESULT_FOCUSER_MAXPOSITION = "RESULT:FOCUSER:MAXPOSITION:";

constexpr auto COMMAND_FOCUSER_SETPOSITION = "COMMAND:FOCUSER:SETPOSITION:";
constexpr auto RESULT_FOCUSER_SETPOSITION = "RESULT:FOCUSER:SETPOSITION:";

constexpr auto COMMAND_FOCUSER_SETREVERSE = "COMMAND:FOCUSER:SETREVERSE:";
constexpr auto RESULT_FOCUSER_SETREVERSE = "RESULT:FOCUSER:SETREVERSE:";

constexpr auto COMMAND_FOCUSER_ISREVERSE = "COMMAND:FOCUSER:ISREVERSE";
constexpr auto RESULT_FOCUSER_ISREVERSE = "RESULT:FOCUSER:ISREVERSE:";

constexpr auto COMMAND_FOCUSER_ISMOVING = "COMMAND:FOCUSER:ISMOVING";
constexpr auto RESULT_FOCUSER_ISMOVING = "RESULT:FOCUSER:ISMOVING:";

constexpr auto COMMAND_FOCUSER_CALIBRATE = "COMMAND:FOCUSER:CALIBRATE";
constexpr auto RESULT_FOCUSER_CALIBRATE = "RESULT:FOCUSER:CALIBRATE:";

constexpr auto COMMAND_FOCUSER_ISCALIBRATING = "COMMAND:FOCUSER:ISCALIBRATING";
constexpr auto RESULT_FOCUSER_ISCALIBRATING = "RESULT:FOCUSER:ISCALIBRATING:";

constexpr auto COMMAND_FOCUSER_SETZEROPOSITION = "COMMAND:FOCUSER:SETZEROPOSITION";
constexpr auto RESULT_FOCUSER_SETZEROPOSITION = "RESULT:FOCUSER:SETZEROPOSITION:";

constexpr auto COMMAND_FOCUSER_MOVE = "COMMAND:FOCUSER:MOVE:";
constexpr auto RESULT_FOCUSER_MOVE = "RESULT:FOCUSER:MOVE:";

constexpr auto COMMAND_FOCUSER_HALT = "COMMAND:FOCUSER:HALT";
constexpr auto RESULT_FOCUSER_HALT = "RESULT:FOCUSER:HALT:";

constexpr auto ERROR_INVALID_COMMAND = "ERROR:INVALID_COMMAND";

const unsigned int EEPROM_MAGIC_NUMBER = 0x12345678;
const unsigned int EEPROM_MAGIC_NUMBER_ADDR = 0;
const unsigned int EEPROM_POSITION_BASE_ADDR = 4;
const unsigned int EEPROM_MAX_STEPS_BASE_ADDR = 8;
const unsigned int EEPROM_REVERSE_BASE_ADDR = 12;

//-- VARIABLES ----------------------------------------------------------------

SoftwareSerial SoftSerial(SW_RX, SW_TX); // RX, TX
TMC2209Stepper driver(&SoftSerial, R_SENSE, DRIVER_ADDRESS);

// While moving, steps_left > 0
// When not moving, steps_left == 0
uint32_t steps_left;

enum Direction {
    forward = 1,
    backward = -1
} direction;

bool is_reverse = false;

// The current position, which we store in EEPROM
uint32_t position;
uint32_t max_steps = 100000;

bool is_manually_jogging = false;
bool jog_direction = false;
int jogStartTime = 0;
const int jogGrace = 5;

bool is_calibrating = false;
bool forward_calibration_direction = true;
int calibration_step;
uint32_t outmost_position;

long acceleration_start_time = 0;

uint32_t ihold   = 2;   // low hold current
uint32_t irun    = 8;  // low run current
uint32_t iholddelay   = 4;   // small delay

const uint8_t stall_guard_threshold = 211;
uint8_t stall_counter = 0;
int start_stall_time = 0;
int stall_delay = -1;
int stall_grace = -1;

// Function declaration
void stalled();
void stallInterrupt();
void calibrate();
bool HandleFreeCommand(String command);
bool HandleBlockingCommand(String command);
uint32_t lerpClamped(uint32_t a, uint32_t b, float t);

bool moveFocuser(long target_position);
void haltFocuser();

void setup() 
{
  delay(2000);

  Serial.begin(9600);

#if defined(DEBUG) && DEBUG != OFF
  Serial.println("Begin setup...");
#endif

  pinMode(EN_PIN, OUTPUT);
  pinMode(DIR_PIN, OUTPUT);
  pinMode(STEP_PIN, OUTPUT);
  pinMode(DIAG_PIN, INPUT);
  pinMode(BUTTON_PIN, INPUT);

  digitalWrite(EN_PIN, HIGH); // Disable

  // Focuser setup
  steps_left = 0;
  direction = forward;
  position = 0;

  // Drive setup
  SoftSerial.begin(DRIVER_SERIAL_BAUD);
  driver.begin();
  driver.toff(4); // Enables driver
  driver.blank_time(24);
  driver.rms_current(400); // mA
  driver.microsteps(4); // Set microsteps

  driver.en_spreadCycle(false); // Disable spreadCycle
  driver.pwm_autoscale(true); // Needed for StealthChop, harmless here
  driver.intpol(1);

  driver.shaft(false);
  driver.irun(irun);
  driver.ihold(ihold);
  driver.iholddelay(iholddelay);
  driver.TCOOLTHRS(0xFFFFF);

  driver.semin(5);
  driver.semax(2);
  driver.sedn(0b01);

  driver.SGTHRS(stall_guard_threshold);

  attachInterrupt(digitalPinToInterrupt(DIAG_PIN), stallInterrupt, RISING);

  delay(500); // Let driver initialize

  if (Serial)
  {
    Serial.flush();
  }

#if defined(DEBUG) && DEBUG != OFF
  Serial.print("Driver version: ");
  Serial.println(driver.version());
#endif

  unsigned int magic_number;
  EEPROM.begin(13);
  EEPROM.get(EEPROM_MAGIC_NUMBER_ADDR, magic_number);
  if (magic_number == EEPROM_MAGIC_NUMBER) {
      // The value stored in EEPROM is trustworthy...
      EEPROM.get(EEPROM_POSITION_BASE_ADDR, position);
      EEPROM.get(EEPROM_MAX_STEPS_BASE_ADDR, max_steps);
      EEPROM.get(EEPROM_REVERSE_BASE_ADDR, is_reverse);
  } else {
      // The position had never been stored in EEPROM. Initialize it to 0...
      position = 0;
      max_steps = 100000;
      is_reverse = false;
      // Store it...
      EEPROM.put(EEPROM_POSITION_BASE_ADDR, position);
      EEPROM.put(EEPROM_MAX_STEPS_BASE_ADDR, max_steps);
      EEPROM.put(EEPROM_REVERSE_BASE_ADDR, is_reverse);
      // And mark the value as trustworthy...
      EEPROM.put(EEPROM_MAGIC_NUMBER_ADDR, EEPROM_MAGIC_NUMBER);

      EEPROM.commit();
  }
}

void loop() 
{
  if(is_calibrating)
  {
    calibrationDelay();
  }
  else
  {
    int now = millis();

    int buttonsState = digitalRead(BUTTON_PIN);
    if (is_manually_jogging && buttonsState == LOW && now - jogStartTime > jogGrace)
    {
      is_manually_jogging = false;
      stop();
    }

    if(!is_manually_jogging && buttonsState == HIGH)
    {
      is_manually_jogging = true;

      if (position >= max_steps) jog_direction = false;
      else if (position <= 0)    jog_direction = true;
      else                       jog_direction = !jog_direction;

      jogStartTime = now;

      if (jog_direction) moveFocuser(max_steps);
      else               moveFocuser(0);
    }
  }

  if (Serial && Serial.available() > 0) 
  {
    String command = Serial.readStringUntil('\n');
    command.replace("\r","");
    
    bool handledFreeCommand = HandleFreeCommand(command);
    bool handledBlockingCommand = true;
    
    bool calibrationState = is_calibrating;
    if (!calibrationState)
    {
      handledBlockingCommand = HandleBlockingCommand(command);
    }

    if ((!handledFreeCommand && calibrationState) 
      || (!handledFreeCommand && !handledBlockingCommand && !calibrationState))
    {
      handleInvalidCommand();
    }
  }

  // Make the stepper motor move, if needed, 1 step at a time...
  step();
}

bool HandleFreeCommand(String command)
{
  if (command == COMMAND_PING) 
  {
    handlePing();
  }
  else if (command == COMMAND_FOCUSER_GETPOSITION) 
  {
    sendFocuserPosition();
  }
  else if (command == COMMAND_FOCUSER_GETMAXPOSITION)
  {
    sendMaxPosition();
  }
  else if (command == COMMAND_INFO) 
  {
    sendFirmwareInfo();
  }
  else if (command == COMMAND_FOCUSER_ISMOVING) 
  {
    sendFocuserState();
  }
  else if (command == COMMAND_FOCUSER_ISREVERSE) 
  {
    sendReverseState();
  }
  else if (command == COMMAND_FOCUSER_ISCALIBRATING) 
  {
    sendIsCalibratingState();
  }
  else if (command == COMMAND_FOCUSER_HALT && is_calibrating) 
  {
    haltFocuser();
  }
  else
  {
    return false;
  }

  return true;
}

bool HandleBlockingCommand(String command)
{
  if (command == COMMAND_FOCUSER_CALIBRATE) 
  {
    calibrate();
  }
  else if (command.startsWith(COMMAND_FOCUSER_SETREVERSE))
  {
    String arg = command.substring(strlen(COMMAND_FOCUSER_SETREVERSE));
    Serial.print(RESULT_FOCUSER_SETREVERSE);
    if (arg != TRUE && arg != FALSE)
    {
      Serial.println(MSG_NOK);
    }
    else
    {
      is_reverse = arg == TRUE;
      EEPROM.put(EEPROM_REVERSE_BASE_ADDR, is_reverse);
      Serial.println(MSG_OK);

      EEPROM.commit();
    }
  }
  else if (command.startsWith(COMMAND_FOCUSER_SETPOSITION)) 
  {
    String arg = command.substring(strlen(COMMAND_FOCUSER_SETPOSITION));
    int value = arg.toInt();
    setFocuserPosition(value);
  }
  else if (command == COMMAND_FOCUSER_SETZEROPOSITION) 
  {
    setFocuserZeroPosition();
  }
  else if (command.startsWith(COMMAND_FOCUSER_MOVE)) 
  {
    String arg = command.substring(strlen(COMMAND_FOCUSER_MOVE));
    int value = arg.toInt();

    Serial.print(RESULT_FOCUSER_MOVE);
    Serial.println(moveFocuser(value) ? MSG_OK : MSG_NOK);
  }
  else if (command == COMMAND_FOCUSER_HALT) 
  {
    haltFocuser();
  }
  else 
  {
    return false;
  }

  return true;
}

//-- UTILITY FUNCTIONS -----------------------------------------------------

void step() 
{
  if (steps_left > 0) 
  {
    steps_left--;

    if (direction == forward) 
    {
      position++;
    }
    else 
    {
      position--;
    }

    long timeSinceStartMove = millis() - acceleration_start_time;

    // compute motor delay (noops)
    uint32_t motorDelay = lerpClamped(MIN_SPEED_DELAY, MAX_SPEED_DELAY, timeSinceStartMove / ACCELERATION_TIME);

    digitalWrite(STEP_PIN, HIGH);
    for (volatile int i = 0; i < motorDelay; i++) { asm volatile("nop"); } // 1000 = ~2-3µs
    digitalWrite(STEP_PIN, LOW);
  }
}

void stop() 
{
    // Make sure we don't take another step.
    steps_left = 0;

    // Store the final position in EEPROM.
    EEPROM.put(EEPROM_POSITION_BASE_ADDR, position);

    // And de-energize the stepper by setting all the pins to LOW to save power,
    // prevent heat build up, and eliminate vibrations.
    digitalWrite(STEP_PIN, LOW);
    digitalWrite(DIR_PIN, LOW);
    digitalWrite(EN_PIN, HIGH); // Disable

    EEPROM.commit();
}

void stallInterrupt()
{
  int now = millis();

#if defined(DEBUG) && DEBUG != OFF
  Serial.print("Stall detected: ");
  Serial.println(now);
#endif

  if (now - stall_grace < STALL_GRACE_PERIOD)
  {
    return;
  }

  if (stall_counter == 0)
  {
    stall_counter++;
    start_stall_time = now;

    return;
  }

  stall_counter++;
  if (now - start_stall_time < STALL_TIME_THRS)
  {
    if(stall_counter < STALL_COUNT_THRESHOLD)
    {
      return;
    }

    stall_counter = 0;

    stalled();

    return;
  }

  if (now - start_stall_time > STALL_TIME_THRS)
  {
    stall_counter = 0; // false stall
    start_stall_time = 0;
  }
}

void stalled()
{
  if (!is_calibrating)
  {
    return;
  }

  if (stall_delay == -1)
  {
    stall_delay = millis();

#if defined(DEBUG) && DEBUG != OFF
    Serial.println("Stalled!");
#endif

    stop();
  }

  if(calibration_step == 1)
  {
    calibrationMoveNext();
  }
}

void calibrationDelay()
{
  int now = millis();
  if(stall_delay == -1 || now - stall_delay < 1500)
  {
    return;
  }

  stall_delay = -1;
  calibrationMoveNext();
}

void calibrate()
{
  is_calibrating = true;
  position = 500000;
  calibration_step = 0;

  EEPROM.put(EEPROM_POSITION_BASE_ADDR, position);

  moveFocuser(1000000); // some huge number

  Serial.print(RESULT_FOCUSER_CALIBRATE);
  Serial.println(MSG_OK);

  EEPROM.commit();
}

void calibrationMoveNext()
{
  if(calibration_step == 0)
  {
    calibration_step = 1;
    outmost_position = position - 20; // some buffer to offset stalls

    stall_delay = -1;
    steps_left = 0;

    moveFocuser(0);

    return;
  }

  if(calibration_step == 1)
  {
    is_calibrating = false;

    max_steps = outmost_position - position + 20;
    position = 0;

    stall_delay = -1;

    EEPROM.put(EEPROM_POSITION_BASE_ADDR, position);
    EEPROM.put(EEPROM_MAX_STEPS_BASE_ADDR, max_steps);

    EEPROM.commit();

    stop();
  }
}

//-- FOCUSER HANDLING ------------------------------------------------------

void sendFocuserState() 
{
    Serial.print(RESULT_FOCUSER_ISMOVING);
    Serial.println(steps_left != 0 ? TRUE : FALSE);
}

void sendReverseState() 
{
    Serial.print(RESULT_FOCUSER_ISREVERSE);
    Serial.println(is_reverse == 1 ? TRUE : FALSE);
}

void sendIsCalibratingState() 
{
    Serial.print(RESULT_FOCUSER_ISCALIBRATING);
    Serial.println(is_calibrating == 1 ? TRUE : FALSE);
}

void sendFocuserPosition() 
{
    Serial.print(RESULT_FOCUSER_POSITION);
    Serial.println(position);
}

void sendMaxPosition() 
{
    Serial.print(RESULT_FOCUSER_MAXPOSITION);
    Serial.println(max_steps);
}

void setFocuserZeroPosition() 
{
    Serial.print(RESULT_FOCUSER_SETZEROPOSITION);
    if (steps_left == 0) {
        position = 0;
        EEPROM.put(EEPROM_POSITION_BASE_ADDR, position);
        Serial.println(MSG_OK);

        EEPROM.commit();
    } else {
        // Cannot set zero position while focuser is still moving...
        Serial.println(MSG_NOK);
    }
}

void setFocuserPosition(int target_position)
{
    Serial.print(RESULT_FOCUSER_SETPOSITION);
    if (steps_left == 0) {
        position = target_position;

        if(target_position > max_steps)
        {
          max_steps = target_position;
          EEPROM.put(EEPROM_MAX_STEPS_BASE_ADDR, max_steps);
        }

        EEPROM.put(EEPROM_POSITION_BASE_ADDR, position);
        Serial.println(MSG_OK);

        EEPROM.commit();
    } else {
        // Cannot set position while focuser is still moving...
        Serial.println(MSG_NOK);
    }
}

bool moveFocuser(long target_position) 
{
  if (steps_left > 0) 
  {
    return false;
  }

  stall_grace = millis();
  acceleration_start_time = millis();

  if (target_position >= position) 
  {
    steps_left = target_position - position;
    digitalWrite(DIR_PIN, is_reverse ? LOW : HIGH);
    direction = forward;
  } 
  else
  {
    steps_left = position - target_position;
    digitalWrite(DIR_PIN, is_reverse ? HIGH : LOW);
    direction = backward;
  }

  digitalWrite(EN_PIN, LOW); // Enable

  return true;
}

void haltFocuser() 
{
    stop();
    Serial.print(RESULT_FOCUSER_HALT);
    Serial.println(MSG_OK);
}

//-- MISCELLANEOUS ------------------------------------------------------------

void handlePing() 
{
    Serial.print(RESULT_PING);
    Serial.println(DEVICE_GUID);
}

void sendFirmwareInfo() 
{
    Serial.println(RESULT_INFO);
}

void handleInvalidCommand()
{
    Serial.println(ERROR_INVALID_COMMAND);
}

uint32_t lerpClamped(uint32_t a, uint32_t b, float t)
{
  if (t <= 0.0f) return a;
  if (t >= 1.0f) return b;

  return static_cast<uint32_t>(a + (static_cast<float>(b) - static_cast<float>(a)) * t);
}