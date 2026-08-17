#include "lcd.h"

// The managed library never supplies an LCD backend. These functions preserve the
// MCU's required peripheral wiring without linking fonts, backgrounds, rendering,
// or display-thread behavior that cannot be observed through the C ABI.
void LCD_Init(lcd_t& lcd, mcu_t& mcu)
{
    lcd.mcu = &mcu;
}

bool LCD_Start(lcd_t& lcd)
{
    return lcd.backend == nullptr;
}

void LCD_Stop(lcd_t&)
{
}

void LCD_Write(lcd_t&, uint32_t, uint8_t)
{
}

void LCD_Enable(lcd_t& lcd, bool enable)
{
    lcd.enable = enable;
}

void LCD_Render(lcd_t&)
{
}
